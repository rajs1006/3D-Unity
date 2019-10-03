using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.IO;
using OpenCvSharp;
using System.Runtime.InteropServices;
// @TODO:
// . support custom color wheels in optical flow via lookup textures
// . support custom depth encoding
// . support multiple overlay cameras
// . tests
// . better example scene(s)

// @KNOWN ISSUES
// . Motion Vectors can produce incorrect results in Unity 5.5.f3 when
//      1) during the first rendering frame
//      2) rendering several cameras with different aspect ratios - vectors do stretch to the sides of the screen

[RequireComponent (typeof(Camera))]
public class ImageSynthesis : MonoBehaviour {

	// pass configuration
	private CapturePass[] capturePasses = new CapturePass[] {
		new CapturePass() { name = "_img" },
		new CapturePass() { name = "_id", supportsAntialiasing = false },
		new CapturePass() { name = "_layer", supportsAntialiasing = false },
		new CapturePass() { name = "_depth" },
		new CapturePass() { name = "_normals" },
		new CapturePass() { name = "_flow", supportsAntialiasing = false, needsRescale = true } // (see issue with Motion Vectors in @KNOWN ISSUES)
	};

	struct CapturePass {
		// configuration
		public string name;
		public bool supportsAntialiasing;
		public bool needsRescale;
		public CapturePass(string name_) { name = name_; supportsAntialiasing = true; needsRescale = false; camera = null; }

		// impl
		public Camera camera;
	};
	
	public Shader uberReplacementShader;
	public Shader opticalFlowShader;

	public float opticalFlowSensitivity = 1.0f;

	// cached materials
	private Material opticalFlowMaterial;

	private ORB orb;

	private Mat projectionMatrix;

	private FastFeatureDetector fast;

	void Awake()
	{
		// default fallbacks, if shaders are unspecified
		if (!uberReplacementShader)
			uberReplacementShader = Shader.Find("Hidden/UberReplacement");

		if (!opticalFlowShader)
			opticalFlowShader = Shader.Find("Hidden/OpticalFlow");

		// use real camera to capture final image
		capturePasses[0].camera = GetComponent<Camera>();
		for (int q = 1; q < capturePasses.Length; q++)
			capturePasses[q].camera = CreateHiddenCamera (capturePasses[q].name);

		OnCameraChange();
		OnSceneChange();
	}

	public Camera getCamera(){
		return capturePasses[0].camera ;
	}

	void LateUpdate()
	{
		#if UNITY_EDITOR
		if (DetectPotentialSceneChangeInEditor())
			OnSceneChange();
		#endif // UNITY_EDITOR

		// @TODO: detect if camera properties actually changed
		OnCameraChange();
	}
	
	private Camera CreateHiddenCamera(string name)
	{
		var go = new GameObject (name, typeof (Camera));
		go.hideFlags = HideFlags.HideAndDontSave;
		go.transform.parent = transform;

		var newCamera = go.GetComponent<Camera>();
		return newCamera;
	}


	static private void SetupCameraWithReplacementShader(Camera cam, Shader shader, ReplacelementModes mode)
	{
		SetupCameraWithReplacementShader(cam, shader, mode, Color.black);
	}

	static private void SetupCameraWithReplacementShader(Camera cam, Shader shader, ReplacelementModes mode, Color clearColor)
	{
		var cb = new CommandBuffer();
		cb.SetGlobalFloat("_OutputMode", (int)mode); // @TODO: CommandBuffer is missing SetGlobalInt() method
		cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
		cam.AddCommandBuffer(CameraEvent.BeforeFinalPass, cb);
		cam.SetReplacementShader(shader, "");
		cam.backgroundColor = clearColor;
		cam.clearFlags = CameraClearFlags.SolidColor;
	}

	static private void SetupCameraWithPostShader(Camera cam, Material material, DepthTextureMode depthTextureMode = DepthTextureMode.None)
	{
		var cb = new CommandBuffer();
		cb.Blit(null, BuiltinRenderTextureType.CurrentActive, material);
		cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
		cam.depthTextureMode = depthTextureMode;
	}

	enum ReplacelementModes {
		ObjectId 			= 0,
		CatergoryId			= 1,
		DepthCompressed		= 2,
		DepthMultichannel	= 3,
		Normals				= 4
	};

	public void OnCameraChange()
	{
		int targetDisplay = 1;
		var mainCamera = GetComponent<Camera>();
		foreach (var pass in capturePasses)
		{
			if (pass.camera == mainCamera)
				continue;

			// cleanup capturing camera
			pass.camera.RemoveAllCommandBuffers();

			// copy all "main" camera parameters into capturing camera
			pass.camera.CopyFrom(mainCamera);

			// set targetDisplay here since it gets overriden by CopyFrom()
			pass.camera.targetDisplay = targetDisplay++;
		}

		// cache materials and setup material properties
		if (!opticalFlowMaterial || opticalFlowMaterial.shader != opticalFlowShader)
			opticalFlowMaterial = new Material(opticalFlowShader);
		opticalFlowMaterial.SetFloat("_Sensitivity", opticalFlowSensitivity);

		// setup command buffers and replacement shaders
		SetupCameraWithReplacementShader(capturePasses[1].camera, uberReplacementShader, ReplacelementModes.ObjectId);
		SetupCameraWithReplacementShader(capturePasses[2].camera, uberReplacementShader, ReplacelementModes.CatergoryId);
		SetupCameraWithReplacementShader(capturePasses[3].camera, uberReplacementShader, ReplacelementModes.DepthCompressed, Color.white);
		SetupCameraWithReplacementShader(capturePasses[4].camera, uberReplacementShader, ReplacelementModes.Normals);
		SetupCameraWithPostShader(capturePasses[5].camera, opticalFlowMaterial, DepthTextureMode.Depth | DepthTextureMode.MotionVectors);
	}


	public void OnSceneChange()
	{
		var renderers = Object.FindObjectsOfType<Renderer>();
		var mpb = new MaterialPropertyBlock();
		foreach (var r in renderers)
		{
			var id = r.gameObject.GetInstanceID();
			var layer = r.gameObject.layer;
			var tag = r.gameObject.tag;

			mpb.SetColor("_ObjectColor", ColorEncoding.EncodeIDAsColor(id));
			mpb.SetColor("_CategoryColor", ColorEncoding.EncodeLayerAsColor(layer));
			r.SetPropertyBlock(mpb);
		}
	}

	public void PointAndPnP(Point2f[] imgPts, Point3f[] objPts){
        projectionMatrix = pnp(objPts, imgPts, getCameraMatrix(capturePasses[0].camera), SolvePnPFlags.EPNP);
		// Debug.Log($"projectionMatrix PointAndPnP   : {projectionMatrix.Get<double>(0,0)} {projectionMatrix.Get<double>(0,1)} {projectionMatrix.Get<double>(0,2)}");
    }

	public void Save(Vector3[] transformed3dKeypoints, Bounds objBound, string filename, int width = -1, int height = -1, string path = "", bool saveOnlyImageAndLayer = false)
	{
		if (width <= 0 || height <= 0)
		{
			width = Screen.width;
			height = Screen.height;
		}

		var filenameExtension = System.IO.Path.GetExtension(filename);
		if (filenameExtension == "")
			filenameExtension = ".png";
		var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

		var pathWithoutExtension = Path.Combine(path, filenameWithoutExtension);

		// execute as coroutine to wait for the EndOfFrame before starting capture
		StartCoroutine(
			WaitForEndOfFrameAndSave(pathWithoutExtension, filenameExtension, width, height, saveOnlyImageAndLayer, transformed3dKeypoints, objBound));
	}

	private IEnumerator WaitForEndOfFrameAndSave(string filenameWithoutExtension, string filenameExtension, int width, int height, bool saveOnlyImageAndLayer, Vector3[] transformed3dKeypoints,Bounds objBound)
	{
		yield return new WaitForEndOfFrame();
		Save(filenameWithoutExtension, filenameExtension, width, height, saveOnlyImageAndLayer, transformed3dKeypoints, objBound);
	}

	private void Save(string filenameWithoutExtension, string filenameExtension, int width, int height, bool saveOnlyImageAndLayer, Vector3[] transformed3dKeypoints, Bounds objBound)
	{
		if(saveOnlyImageAndLayer){
			var passImg = capturePasses[0];
			Save(passImg.camera, filenameWithoutExtension + passImg.name + filenameExtension, width, height, passImg.supportsAntialiasing, passImg.needsRescale, transformed3dKeypoints, objBound);
			//var passLayer = capturePasses[2];
			//Save(passLayer.camera, filenameWithoutExtension + passLayer.name + filenameExtension, width, height, passLayer.supportsAntialiasing, passLayer.needsRescale, mesharray);
		}else{
			foreach (var pass in capturePasses)
				Save(pass.camera, filenameWithoutExtension + pass.name + filenameExtension, width, height, pass.supportsAntialiasing, pass.needsRescale, transformed3dKeypoints, objBound);
		}
	}

	private void Save(Camera cam, string filename, int width, int height, bool supportsAntialiasing, bool needsRescale, Vector3[] transformed3dKeypoints, Bounds objBound)
	{
		//Debug.Log($"mesharray.Length {mesharray.Length}");
		var mainCamera = GetComponent<Camera>();
		var depth = 24;
		var format = RenderTextureFormat.Default;
		var readWrite = RenderTextureReadWrite.Default;
		var antiAliasing = (supportsAntialiasing) ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;

		var finalRT =
			RenderTexture.GetTemporary(width, height, depth, format, readWrite, antiAliasing);
		var renderRT = (!needsRescale) ? finalRT :
			RenderTexture.GetTemporary(mainCamera.pixelWidth, mainCamera.pixelHeight, depth, format, readWrite, antiAliasing);
		//var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

		var prevActiveRT = RenderTexture.active;
		var prevCameraRT = cam.targetTexture;
		var rTex = renderRT;
		// render to offscreen texture (readonly from CPU side)
		RenderTexture.active = renderRT;
		cam.targetTexture = renderRT;
		
		cam.Render();

		if (needsRescale)
		{
			// blit to rescale (see issue with Motion Vectors in @KNOWN ISSUES)
			RenderTexture.active = finalRT;
			Graphics.Blit(renderRT, finalRT);
			rTex = finalRT;
			RenderTexture.ReleaseTemporary(renderRT);
		}

		var tex = rTex.toTexture2D();
        Color32[] texC = tex.GetPixels32();
	    //System.Array.Reverse(texC);
		
		int nKeyPoints = transformed3dKeypoints.Length;
		if(nKeyPoints == 0){
			nKeyPoints = 21;
		}

        orb = ORB.Create(nKeyPoints);

        Mat camMat = new Mat(cam.pixelHeight, cam.pixelWidth, MatType.CV_8UC4, texC);
    
        KeyPoint[] keyPoints = null;
        keyPoints = orb.Detect(camMat);
		
		var keypointFileName = filename.Replace(Path.GetExtension(filename), "-ORB.txt");
		using(TextWriter kpWriter = new StreamWriter(keypointFileName))
		{
			foreach (var k in keyPoints)
			{
				// In python, CNN, the Y-axis, is inverted and so height - Y
				kpWriter.WriteLine(k.Pt.X + "," + k.Pt.Y);
			}
			kpWriter.Flush();
		}

		var groundTruthkeypointFileName = filename.Replace(Path.GetExtension(filename), "-GT.txt");
		using(TextWriter gdKpWriter = new StreamWriter(groundTruthkeypointFileName))
		{
			foreach (var kp3D in transformed3dKeypoints)
			{
				// In python, CNN, the Y-axis, is inverted and so height - Y
				Vector3 gdKeyPoints = project3DPoints(kp3D, cam);
				gdKpWriter.WriteLine(gdKeyPoints.x + "," + gdKeyPoints.y);
			}
			gdKpWriter.Flush();
		}

		var objBoundFileName = filename.Replace(Path.GetExtension(filename), "-BOUND.txt");
		using(TextWriter objBoundWriter = new StreamWriter(objBoundFileName))
		{
			Vector2[] bound = ScreenScaleOfObject(objBound, cam);
			foreach(Vector2 b in bound){
				objBoundWriter.WriteLine(b.x + "," + b.y);
			}
			objBoundWriter.Flush();
		}

		Mat afterMat= new Mat();
        Cv2.DrawKeypoints(camMat, keyPoints, afterMat, new Scalar(0, 0, 255), 0);

        var cTex = MatToTexture(camMat, tex);
		var aTex = MatToTexture(afterMat, tex);

		// encode texture into PNG
		var bytes = cTex.EncodeToPNG();
		File.WriteAllBytes(filename, bytes);	

		var abytes = aTex.EncodeToPNG();
		File.WriteAllBytes(filename.Replace(Path.GetExtension(filename), "-kp.png"), abytes);

		cam.targetTexture = prevCameraRT;
		RenderTexture.active = prevActiveRT;

		Object.Destroy(tex);
		RenderTexture.ReleaseTemporary(finalRT);
	}

	private Vector2[] ScreenScaleOfObject(Bounds objBound, Camera cam)
    {
        Vector3 cen = objBound.center;
        Vector3 ext = objBound.extents;

        Vector2[] extentPoints = new Vector2[8]
        {
            cam.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y-ext.y, cen.z-ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y-ext.y, cen.z-ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y-ext.y, cen.z+ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y-ext.y, cen.z+ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y+ext.y, cen.z-ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y+ext.y, cen.z-ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y+ext.y, cen.z+ext.z)),
            cam.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y+ext.y, cen.z+ext.z))
        };

        Vector2 min = extentPoints[0];
        Vector2 max = extentPoints[0];
        foreach(Vector2 v in extentPoints)
        {
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }

        // Debug.Log($" Screen Pos : ({min.x} {min.y}) ({max.x} {max.y})");

		Vector2[] bound = new Vector2[2]
		{
			new Vector2(min.x, min.y),
			new Vector2(max.x, max.y)
		};

		return bound;
    }

	 public Texture2D MatToTexture(Mat mat, Texture2D outTexture = null)
    {
        Size size = mat.Size();
        
        if (null == outTexture || outTexture.width != size.Width || outTexture.height != size.Height)
            Debug.Log($"outTexture {outTexture.width} : {size.Width}");
            outTexture = new Texture2D(size.Width, size.Height, TextureFormat.RGBA32, false);

        int count = size.Width * size.Height;
        Color32Bytes data = new Color32Bytes();
        data.byteArray = new byte[count * 4];
        data.colors = new Color32[count];
        Marshal.Copy(mat.Data, data.byteArray, 0, data.byteArray.Length);
        outTexture.LoadRawTextureData(data.byteArray);
        //outTexture.SetPixels32(data.colors);
        outTexture.Apply();

        return outTexture;
        
    }

	private double[, ] getCameraMatrix(Camera cam){
		
		var f = cam.focalLength;
        var sensorSize = cam.sensorSize;

        var sx = sensorSize.x;
        var sy =  sensorSize.y;
        var width = cam.pixelWidth;
        var height = cam.pixelHeight;

        Debug.Log(" f " + f + "  sensor " +  sensorSize + " : "+sx+ " width/height " + width + "  :  " + height);

        double[, ] cameraMatrix = new double[3, 3]
        {
            { (width*f)/sx, 0, width/2 },
            { 0, (height*f)/sy, height/2 },
            { 0, 0, 1}
        };

		return cameraMatrix;
	}

	private Vector3 project3DPoints(Vector3 obj, Camera cam){

        Mat point2d_vec = new Mat(4, 1, MatType.CV_64FC1);

        Mat cameraMatrixMat = new Mat(3, 3, MatType.CV_64FC1, getCameraMatrix(cam));

        Mat point3d_vec = new Mat(4, 1, MatType.CV_64FC1);
        point3d_vec.Set<double>(0, obj.x);
        point3d_vec.Set<double>(1, obj.y);
        point3d_vec.Set<double>(2, obj.z);
        point3d_vec.Set<double>(3, 1);

		Debug.Log($"projectionMatrix project3DPoints   : {projectionMatrix.Get<double>(0,0)} {projectionMatrix.Get<double>(0,1)} {projectionMatrix.Get<double>(0,2)}");
        point2d_vec = cameraMatrixMat * projectionMatrix * point3d_vec;

        var X2D = point2d_vec.At<double>(0)/point2d_vec.At<double>(2);
        var Y2D = point2d_vec.At<double>(1)/point2d_vec.At<double>(2);

        Debug.Log(" 3d Points " + obj.x + " " + obj.y + " " + obj.z);
        Debug.Log(" 2d point  x : " + X2D + " y " + Y2D);
        //Debug.Log(" float 2d point  x : " + (float)X2D + " y " + (float)Y2D);

        Vector3 pnpPoints = new Vector3((float)X2D, (float)Y2D, 0.0f);

        return pnpPoints;
    }

	private Mat pnp(Point3f[] objPts, Point2f[] imgPts,double[, ] cameraMatrix, SolvePnPFlags type){

        
        using (var objPtsMat = new Mat(objPts.Length, 1, MatType.CV_32FC3, objPts))
        using (var imgPtsMat = new Mat(imgPts.Length, 1, MatType.CV_32FC2, imgPts))
        using (var cameraMatrixMat = new Mat(3, 3, MatType.CV_64FC1, cameraMatrix))
        using (var distMat = Mat.Zeros(5, 0, MatType.CV_64FC1))
        using (var rvecMat = new Mat())
        using (var tvecMat = new Mat())
        {
            
            Cv2.SolvePnP(objPtsMat, imgPtsMat, cameraMatrixMat, distMat, rvecMat, tvecMat, flags : type);
            //Debug.Log("Solved " + rvecMat + "  :  "+tvecMat);

            Mat rot_matrix = Mat.Zeros(3, 3, MatType.CV_64FC1);
            Mat tr_matrix = Mat.Zeros(3, 1, MatType.CV_64FC1);
            Mat proj_matrix = Mat.Zeros(3, 4, MatType.CV_64FC1);

            Cv2.Rodrigues(rvecMat, rot_matrix);
            tr_matrix = tvecMat;

            proj_matrix.Set<double>(0, 0, rot_matrix.At<double>(0,0));
            proj_matrix.Set<double>(0, 1,rot_matrix.At<double>(0,1));
            proj_matrix.Set<double>(0, 2,rot_matrix.At<double>(0,2));

            proj_matrix.Set<double>(1, 0,rot_matrix.At<double>(1,0));
            proj_matrix.Set<double>(1, 1,rot_matrix.At<double>(1,1));
            proj_matrix.Set<double>(1, 2,rot_matrix.At<double>(1,2));

            proj_matrix.Set<double>(2, 0,rot_matrix.At<double>(2,0));
            proj_matrix.Set<double>(2, 1,rot_matrix.At<double>(2,1));
            proj_matrix.Set<double>(2, 2,rot_matrix.At<double>(2,2));

            proj_matrix.Set<double>(0, 3,tr_matrix.At<double>(0));
            proj_matrix.Set<double>(1, 3,tr_matrix.At<double>(1));
            proj_matrix.Set<double>(2, 3,tr_matrix.At<double>(2));

			return proj_matrix;
        }
    }

	#if UNITY_EDITOR
	private GameObject lastSelectedGO;
	private int lastSelectedGOLayer = -1;
	private string lastSelectedGOTag = "unknown";
	private bool DetectPotentialSceneChangeInEditor()
	{
		bool change = false;
		// there is no callback in Unity Editor to automatically detect changes in scene objects
		// as a workaround lets track selected objects and check, if properties that are 
		// interesting for us (layer or tag) did not change since the last frame
		if (UnityEditor.Selection.transforms.Length > 1)
		{
			// multiple objects are selected, all bets are off!
			// we have to assume these objects are being edited
			change = true;
			lastSelectedGO = null;
		}
		else if (UnityEditor.Selection.activeGameObject)
		{
			var go = UnityEditor.Selection.activeGameObject;
			// check if layer or tag of a selected object have changed since the last frame
			var potentialChangeHappened = lastSelectedGOLayer != go.layer || lastSelectedGOTag != go.tag;
			if (go == lastSelectedGO && potentialChangeHappened)
				change = true;

			lastSelectedGO = go;
			lastSelectedGOLayer = go.layer;
			lastSelectedGOTag = go.tag;
		}

		return change;
	}
	#endif // UNITY_EDITOR
}

public static class ExtensionMethod
{
    public static Texture2D toTexture2D(this RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    public static RenderTexture toRenderedTexture(this Texture2D tex, RenderTexture rTex)
    {
        
        RenderTexture rt = RenderTexture.GetTemporary(rTex.descriptor);
        //rt.Create();
        
        RenderTexture.active = rt;
         // Copy your texture ref to the render texture
        Graphics.Blit(tex, rt);
        Debug.Log($"rt {rt}");
        Debug.Log($"rTex {rTex}");
        return rTex;
    }
}

public class Color32Bytes
{
    public byte[] byteArray;
    public Color32[] colors;
}
