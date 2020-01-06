using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
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

[RequireComponent(typeof(Camera))]
public class ImageSynthesis : MonoBehaviour
{

    // pass configuration
    private CapturePass[] capturePasses = new CapturePass[] {
        new CapturePass() { name = "_img" },
        new CapturePass() { name = "_id", supportsAntialiasing = false },
        new CapturePass() { name = "_layer", supportsAntialiasing = false },
        new CapturePass() { name = "_depth" },
        new CapturePass() { name = "_normals" },
        new CapturePass() { name = "_flow", supportsAntialiasing = false, needsRescale = true } // (see issue with Motion Vectors in @KNOWN ISSUES)
	};

    struct CapturePass
    {
        // configuration
        public string name;
        public bool supportsAntialiasing;
        public bool needsRescale;
        public CapturePass(string name_) { name = name_; supportsAntialiasing = true; needsRescale = false; camera = null; }
        // impl
        public Camera camera;
    };

    public CVMapping openCV;
    public Shader uberReplacementShader;
    public Shader opticalFlowShader;
    public float opticalFlowSensitivity = 1.0f;
    public int pythonConnectorPort;
    public bool realTime = false;

    // cached materials
    private Material opticalFlowMaterial;
    private PythonConnector pythonConnector;

    void Awake()
    {
        pythonConnector = new PythonConnector();

        if (realTime){
            pythonConnector.start(pythonConnectorPort);
        }

        // default fallbacks, if shaders are unspecified
        if (!uberReplacementShader)
            uberReplacementShader = Shader.Find("Hidden/UberReplacement");

        if (!opticalFlowShader)
            opticalFlowShader = Shader.Find("Hidden/OpticalFlow");

        // use real camera to capture final image
        capturePasses[0].camera = GetComponent<Camera>();
        for (int q = 1; q < capturePasses.Length; q++)
            capturePasses[q].camera = CreateHiddenCamera(capturePasses[q].name);

        OnCameraChange();
        OnSceneChange();
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
        var go = new GameObject(name, typeof(Camera));
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

    enum ReplacelementModes
    {
        ObjectId = 0,
        CatergoryId = 1,
        DepthCompressed = 2,
        DepthMultichannel = 3,
        Normals = 4
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
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
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

    public void stopExecution(){
        if(realTime){
            pythonConnector.close();
        }
    }

    public void PointAndPnP(Point2f[] imgPts, Point3f[] objPts)
    {
        openCV.pnp(objPts, imgPts, capturePasses[0].camera, SolvePnPFlags.Iterative);    

        TextWriter camMatrixFile = new StreamWriter("captures/GroundTruth/CameraMatrix.txt");
        double[,] camMatrix = openCV.getCameraMatrix(capturePasses[0].camera);
            
        camMatrixFile.WriteLine(camMatrix[0, 0] + ", " + camMatrix[0, 1] + ", " +camMatrix[0, 2]);
        camMatrixFile.WriteLine(camMatrix[1, 0] + ", " + camMatrix[1, 1] + ", " +camMatrix[1, 2]);
        camMatrixFile.WriteLine(camMatrix[2, 0] + ", " + camMatrix[2, 1] + ", " +camMatrix[2, 2]);
               
        camMatrixFile.Flush();
        Debug.Log($"projectionMatrix PointAndPnP ");
    }

    public void Save(Vector3[] transformed3dKeypoints, Bounds objBound, string filename, int width = -1, int height = -1, string path = "", bool saveOnlyImageAndLayer = false, bool control = false)
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
            WaitForEndOfFrameAndSave(pathWithoutExtension, filenameExtension, width, height, saveOnlyImageAndLayer, transformed3dKeypoints, objBound, control));
    }

    private IEnumerator WaitForEndOfFrameAndSave(string filenameWithoutExtension, string filenameExtension, int width, int height, bool saveOnlyImageAndLayer, Vector3[] transformed3dKeypoints, Bounds objBound, bool control)
    {
        yield return new WaitForEndOfFrame();
        Save(filenameWithoutExtension, filenameExtension, width, height, saveOnlyImageAndLayer, transformed3dKeypoints, objBound, control);
    }

    private void Save(string filenameWithoutExtension, string filenameExtension, int width, int height, bool saveOnlyImageAndLayer, Vector3[] transformed3dKeypoints, Bounds objBound, bool control)
    {
        if (saveOnlyImageAndLayer)
        {
            var passImg = capturePasses[0];
            Save(passImg.camera, filenameWithoutExtension + passImg.name + filenameExtension, width, height, passImg.supportsAntialiasing, passImg.needsRescale, transformed3dKeypoints, objBound, control);
            //var passLayer = capturePasses[2];
            //Save(passLayer.camera, filenameWithoutExtension + passLayer.name + filenameExtension, width, height, passLayer.supportsAntialiasing, passLayer.needsRescale, mesharray);
        }
        else
        {
            foreach (var pass in capturePasses)
                Save(pass.camera, filenameWithoutExtension + pass.name + filenameExtension, width, height, pass.supportsAntialiasing, pass.needsRescale, transformed3dKeypoints, objBound, control);
        }
    }

    private void Save(Camera cam, string filename, int width, int height, bool supportsAntialiasing, bool needsRescale, Vector3[] transformed3dKeypoints, Bounds objBound, bool control)
    {
        //Debug.DrawLine(new Vector3(0,0,0), new Vector3(0,50,0), Color.black);
        
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

        var objBoundFileName = filename.Replace(Path.GetExtension(filename), "-BOUND.txt");
        using (TextWriter objBoundWriter = new StreamWriter(objBoundFileName))
        {
            Vector2[] bound = openCV.screenScaleOfObject(objBound, cam);
            foreach (Vector2 b in bound)
            {
                objBoundWriter.WriteLine(b.x + "," + (height - b.y));
            }
            objBoundWriter.Flush();
        }

        /**This is where we save the original image */
            //System.Array.Reverse(texC);
        Mat camMat = new Mat(cam.pixelHeight, cam.pixelWidth, MatType.CV_8UC4, texC);
        var cTex = openCV.MatToTexture(camMat, tex);
        // encode texture into PNG
        var bytes = cTex.EncodeToPNG();
        File.WriteAllBytes(filename, bytes);

        int nKeyPoints = transformed3dKeypoints.Length;
        if(!control && nKeyPoints == 0){

            Mat afterMat = new Mat();
            // In this case we take ORB created keypoints as Ground truth keypoint
            KeyPoint[] keyPoints = openCV.getKeyPoints(camMat, 18);
            Cv2.DrawKeypoints(camMat, keyPoints, afterMat, new Scalar(0, 0, 255), 0);

            var aTex = openCV.MatToTexture(afterMat, tex);
            var kpBytes = aTex.EncodeToPNG();
            File.WriteAllBytes(filename.Replace(Path.GetExtension(filename), "-GT.png"), kpBytes);
            
            var keypointFileName = filename.Replace(Path.GetExtension(filename), "-GT.txt");
            using (TextWriter kpWriter = new StreamWriter(keypointFileName))
            {
                foreach (var k in keyPoints)
                {

                    Vector3 kFar = new Vector3(k.Pt.X, k.Pt.Y, cam.farClipPlane);
                    Vector3 kNear = new Vector3(k.Pt.X, k.Pt.Y, cam.nearClipPlane);

                    Vector3 keypointPosF = cam.ScreenToWorldPoint(kFar);
                    Vector3 keypointPosN = cam.ScreenToWorldPoint(kNear);

                    Debug.DrawRay(keypointPosN, keypointPosF - keypointPosN, Color.green);

                    RaycastHit hit;

                    if (Physics.Raycast(keypointPosN, keypointPosF - keypointPosN, out hit))
                    {
                        kpWriter.WriteLine("("+k.Pt.X + ", " + k.Pt.Y + ") : " + (hit.point - hit.transform.position));
                    }
                }
                kpWriter.Flush();
            }
        }else if (nKeyPoints != 0)
        {

            // Saving ground truth keypoints based on transformaition of object
            var gtKeyPoints = new System.Text.StringBuilder();
            var PnPTruthkeypointFileName = filename.Replace(Path.GetExtension(filename), "-PnP.txt");
            var groundTruthkeypointFileName = filename.Replace(Path.GetExtension(filename), "-GT.txt");
            using (TextWriter gdKpWriter = new StreamWriter(groundTruthkeypointFileName))
            using (TextWriter pnpKpWriter = new StreamWriter(PnPTruthkeypointFileName))
            {
                int i = 0;
                foreach (var kp3D in transformed3dKeypoints)
                {
                    // In python, CNN, the Y-axis, is inverted and so height - Y
                    Vector3 pnpKeyPoints = openCV.project3DPoints(kp3D, cam);
                    pnpKpWriter.WriteLine(pnpKeyPoints.x + "," + (height - pnpKeyPoints.y));

                    Vector3 gdKeyPoints = cam.WorldToScreenPoint(kp3D);
                    gtKeyPoints.AppendLine(gdKeyPoints.x + "," + (height - gdKeyPoints.y));
                    gdKpWriter.WriteLine(gdKeyPoints.x + "," + (height - gdKeyPoints.y));
                }
                gdKpWriter.Flush();
                pnpKpWriter.Flush();
            }
            
            /**This is where we save the  image with keypoints */
            Mat afterMat = new Mat();

            KeyPoint[] keyPoints = openCV.getKeyPoints(camMat, nKeyPoints*10);
            Cv2.DrawKeypoints(camMat, keyPoints, afterMat, new Scalar(0, 0, 255), 0);

            var aTex = openCV.MatToTexture(afterMat, tex);
            var kpBytes = aTex.EncodeToPNG();
            File.WriteAllBytes(filename.Replace(Path.GetExtension(filename), "-ORB.png"), kpBytes);

            var cvKeyPoints = new System.Text.StringBuilder();
            var keypointFileName = filename.Replace(Path.GetExtension(filename), "-ORB.txt");
            using (TextWriter kpWriter = new StreamWriter(keypointFileName))
            {
                foreach (var k in keyPoints)
                {
                    // In python, CNN, the Y-axis, is inverted and so height - Y
                    cvKeyPoints.AppendLine(k.Pt.X + "," + (height - k.Pt.Y));
                    kpWriter.WriteLine(k.Pt.X + "," + (height - k.Pt.Y));
                }
                kpWriter.Flush();
            }

            if(realTime){
                
                Mat cnnAfterMat = new Mat();
                camMat.CopyTo(cnnAfterMat);

                Point[] cnnKeyPoints = new Point[nKeyPoints];
                //Debug.Log($"TRain  {filename.Contains("Train")}");
                pythonConnector.sendMore($"{Path.GetFullPath(filename)}", gtKeyPoints.ToString(), cvKeyPoints.ToString(), control);
                string nnKeyPoints = pythonConnector.recieve();

                string[] spearator = { "["," ","\n","]" };
                var nnKPs = nnKeyPoints.Split(spearator, StringSplitOptions.RemoveEmptyEntries);

                var cnnKeypointFileName = filename.Replace(Path.GetExtension(filename), "-CNN.txt");
                using (TextWriter cnnKpWriter = new StreamWriter(cnnKeypointFileName))
                {
                    for(int i=0; i < nnKPs.Length ; i = i+2){
                        //Debug.Log($"k  : {nnKPs[i]} : {nnKPs[i+1]}");
                        cnnKeyPoints[i/2].X = (int)Math.Round(float.Parse(nnKPs[i]));
                        cnnKeyPoints[i/2].Y = height - (int)Math.Round(float.Parse(nnKPs[i + 1]));

                        cnnKpWriter.WriteLine(nnKPs[i] + "," + nnKPs[i + 1]);
                    }
                    cnnKpWriter.Flush();
                }

                for(int p=0; p<cnnKeyPoints.Length - 1 ; p++){
                    for (int j=p; j<p+2 ; j = j+2){
                        //Debug.Log($"drwaing line...{cnnKeyPoints[j]}  {cnnKeyPoints[j+1]}");
                        Cv2.Line(cnnAfterMat, cnnKeyPoints[j],cnnKeyPoints[j+1], new Scalar(0, 0, 255));
                    }
                }

                //Debug.Log($"cnnKeyPoints  : {cnnKeyPoints[1]}");
                var cnnTex = openCV.MatToTexture(cnnAfterMat, tex);
                var cnnBytes = cnnTex.EncodeToPNG();
                File.WriteAllBytes(filename.Replace(Path.GetExtension(filename), "-CNN.png"), cnnBytes);
            }
        }
        
        cam.targetTexture = prevCameraRT;
        RenderTexture.active = prevActiveRT;

        UnityEngine.Object.Destroy(tex);
        RenderTexture.ReleaseTemporary(finalRT);
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


