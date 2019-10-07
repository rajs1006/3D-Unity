

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;

public class SceneController : MonoBehaviour
{
    // public ImageSynthesis synth;
    // public GameObject[] prefabs;
    // public GameObject[] cadPictures;
    // public SampleCountController count;
    // public bool saveImg = false;

    // private int minObject;
    // private int maxObject;

    // private int minCADObject;
    // private int maxCADObject;
    // private int trainingImages;
    // private int testImages;

    // private ShapePool pool;
    // private ShapePool cadPool;
    // private int frameCount = 0;

    // Vector3[] mesharray;

    // private Camera cam;
    // private ORB orb;
    //private WebCamTexture cam;
    // Start is called before the first frame update
    void Start()
    {
        //Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        //Mesh holderMesh = new Mesh();
        //ObjImporter newMesh = new ObjImporter();
        //holderMesh = newMesh.ImportFile("/home/sourabh/Documents/TU-Berlin/Thesis/Sytheticdata/ml-imagesynthesis/Assets/CADPictures/DMU50.obj");
        //MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        //filter.mesh = holderMesh;
        //Debug.Log($"holderMesh {holderMesh.colors32}");


        // minObject = count.minObject;
        // maxObject = count.maxObject;
        // minCADObject = count.minCADObject;
        // maxCADObject = count.maxCADObject;
        // trainingImages = count.trainingImages;
        // testImages = count.testImages;

        // pool = ShapePool.create(prefabs);
        // cadPool = ShapePool.create(cadPictures);

        // mesharray = new Vector3[maxCADObject];
    }

    // Update is called once per frame
    void Update()
    {
        // cam = GetComponent<Camera>();
        // if (frameCount < trainingImages + testImages)
        // {
        //     Debug.Log($"save count {frameCount}");
        //     if (frameCount % 10 == 0)
        //     {
        //         generateCADObjects();
        //         //generate3DObjects();
                
        //         //draw(cam);
                
        //         //Debug.Log($"FrameCount {frameCount}");
        //     }
        //     frameCount++;
        //     if (saveImg)
        //     {
        //         if (frameCount < trainingImages)
        //         {
        //             string filename = $"image_{frameCount.ToString().PadLeft(5, '0')}";
        //             synth.Save(mesharray, this.GetComponent<Collider>().bounds, filename, 1024, 768, "captures/Train", true);
        //         }
        //         else if (frameCount < trainingImages + testImages)
        //         {
        //             int testFrameCount = frameCount - trainingImages;
        //             string filename = $"image_{testFrameCount.ToString().PadLeft(5, '0')}";
        //             synth.Save(mesharray, this.GetComponent<Collider>().bounds, filename, 512, 512, "captures/Test", true);
        //         }
        //     }
        // }
        // else
        // {
        //     // Pause the application after completion of data generation.
        //     Debug.Break();
        // }
    }

    // void generateCADObjects()
    // {
    //     // Marking all the object as Incative.
    //     cadPool.reclaimAll();
    //     // number of object need to be instantiated
    //     int objectCount = Random.Range(minCADObject, maxCADObject);
    //     // pick random cad picture
    //     for (int i = 0; i < objectCount; i++)
    //     {
    //         int cadIndex = Random.Range(0, cadPictures.Length);

    //         // position
    //         float newX, newY, newZ;
    //         newX = Random.Range(0.0f, 20.0f);
    //         newY = Random.Range(10.0f, 10.5f);
    //         newZ = Random.Range(0.0f, 20.0f);

    //         Vector3 newPosition = new Vector3(newX, newY, newZ);
    //         // Rotation
    //         var newRot = Random.rotation;
    //         // Instantiate new Object from the pool.
    //         var shape = cadPool.get(cadIndex);
    //         var newObj = shape.obj;

    //         newObj.transform.position = newPosition;
    //         //newObj.transform.rotation = newRot;
    //         //Debug.Log($"newObj.transform.position : {cam.ViewportToWorldPoint(newObj.transform.position)}");
    //         // color
    //         float newRed, newBlue, newGreen;

    //         newRed = Random.Range(0.0f, 1.0f);
    //         newBlue = Random.Range(0.0f, 1.0f);
    //         newGreen = Random.Range(0.0f, 1.0f);

    //         var newColor = new Color(newRed, newBlue, newGreen);

    //         newObj.GetComponent<Renderer>().material.color = newColor;

    //         // Mesh mesh  = newObj.GetComponent<MeshFilter>().mesh;
    //         // Vector3[] v = mesh.vertices;

    //         // Mat cadMat = new Mat(cam.pixelHeight, cam.pixelWidth, MatType.CV_8UC4, v);

    //         // mesharray[i] = cadMat;
    //         // //Debug.Log($"vertices MAT : {cadMat.Size()}");
    //         // //Debug.Log($"Mesh count {v}");
    //         // foreach (var vt in v)
    //         // {
    //         //     Debug.Log($"mesh : {mesh.name} : vertices : {vt} -> {cam.WorldToScreenPoint(transform.TransformPoint(vt))}");
    //         // }
            
    //     }
    //     synth.OnSceneChange();
    // }

    // public Texture2D MatToTexture(Mat mat, Texture2D outTexture = null)
    // {
    //     Size size = mat.Size();
        
    //     if (null == outTexture || outTexture.width != size.Width || outTexture.height != size.Height)
    //         Debug.Log($"outTexture {outTexture.width} : {size.Width}");
    //         outTexture = new Texture2D(size.Width, size.Height, TextureFormat.RGBA32, false);

    //     int count = size.Width * size.Height;
    //     Color32Bytes data = new Color32Bytes();
    //     data.byteArray = new byte[count * 4];
    //     data.colors = new Color32[count];
    //     Marshal.Copy(mat.Data, data.byteArray, 0, data.byteArray.Length);
    //     outTexture.LoadRawTextureData(data.byteArray);
    //     //outTexture.SetPixels32(data.colors);
    //     outTexture.Apply();

    //     return outTexture;
        
    // }

    // void generate3DObjects()
    // {
    //     pool.reclaimAll();
    //     int objectCount = Random.Range(minObject, maxObject);
    //     // pick random prefab
    //     for (int i = 0; i < objectCount; i++)
    //     {
    //         int prefabIndex = Random.Range(0, prefabs.Length);

    //         // position
    //         float newX, newY, newZ;
    //         newX = Random.Range(-30.0f, 30.0f);
    //         newY = Random.Range(2.0f, 10.0f);
    //         newZ = Random.Range(-30.0f, 30.0f);

    //         Vector3 newPosition = new Vector3(newX, newY, newZ);
    //         // Rotation
    //         var newRot = Random.rotation;
    //         // Instantiate new Object from the pool.
    //         var shape = pool.get(prefabIndex);
    //         var newObj = shape.obj;

    //         newObj.transform.position = newPosition;
    //         newObj.transform.rotation = newRot;

    //         //scale
    //         float sx = Random.Range(5f, 10.0f);
    //         Vector3 newScale = new Vector3(sx, sx, sx);

    //         newObj.transform.localScale = newScale;

    //         // color
    //         float newRed, newBlue, newGreen;

    //         newRed = Random.Range(0.0f, 1.0f);
    //         newBlue = Random.Range(0.0f, 1.0f);
    //         newGreen = Random.Range(0.0f, 1.0f);

    //         var newColor = new Color(newRed, newBlue, newGreen);
    //     }
    //     synth.OnSceneChange();
    // }

    
    //[DllImport("liborb2")]
    //public static extern void run(Mat img, int sigma);

    //[DllImport("liborb2")]
    //public static extern void showImage(Mat img, char win, int wait, bool show, bool save);

   

}



