using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using OpenCvSharp.Dnn;

public class SingleSceneController : MonoBehaviour
{
    public ImageSynthesis synth;
    public GameObject cadObj;
    public GameObject[] prefabs;
    public SampleCountController count;
    public enum Actions
    {
        none,
        generate,
        control,
        convert
    };

    public enum Methods
    {
        none,
        manual,
        random,
        orb
    };
    public Actions action;
    public Methods method;
    private int frameCount = 0;
    private int trainingImages;
    private int testImages;
    private int height;
    private int width;
    private Vector3 initPos;
    private Quaternion initRot;
    private TextWriter tw;
    private Point2f[] imgPts;
    private Point3f[] objPts;
    private string gtKpFile = "captures/GroundTruth/image_groundtruth_img-GT.txt";
    private int kpLen = 21;
    private bool generate = false;
    private bool control = false;
    private bool convert = false;

    private ShapePool pool;
    

    // Start is called before the first frame update
    void Start()
    {

        height = Camera.main.pixelHeight;
        width = Camera.main.pixelWidth;
        Debug.Log("Height : " + height + "  Width : " + width);

        trainingImages = count.trainingImages;
        testImages = count.testImages;

        initPos = cadObj.transform.position;
        initRot = cadObj.transform.rotation;

        decideAction();
        decideMethod();

        if (generate)
        {
            if (method == Methods.manual)
            {
                tw = new StreamWriter(gtKpFile);
            }

            var mesh = cadObj.GetComponent<MeshCollider>().sharedMesh;
            var vertices3D = mesh.vertices;
            var normal3D = mesh.triangles;

            TextWriter vertices = new StreamWriter("captures/GroundTruth/image_groundtruth_img-vertices.txt");
            for (int i = 0; i < vertices3D.Length; i++) 
                {
                    Vector3 v =  cadObj.transform.rotation * vertices3D[i];
                    v = Vector3.Scale(v , cadObj.transform.localScale); 
                    
                    vertices.WriteLine(v.x + ", " + v.y + ", " +v.z);
                }
            Debug.Log($"vertices saved");
            vertices.Flush();

            TextWriter faces = new StreamWriter("captures/GroundTruth/image_groundtruth_img-faces.txt");
            for (int i = 0; i < normal3D.Length; i=i+3) 
                {
                    faces.WriteLine(normal3D[i] + ", " + normal3D[i+1] + ", " +normal3D[i+2]);
                }
            faces.Flush();
        }
        else if (control || convert)
        {
            pool = ShapePool.create(prefabs);
            string[] lines = File.ReadAllLines(gtKpFile);

            kpLen = lines.Length;
            if (kpLen < 2)
            {
                throw new Exception("Minimum 2 Keypoints need to be generated, try action Generate");
            }

            int i = 0;

            imgPts = new Point2f[kpLen];
            objPts = new Point3f[kpLen];

            string[] spearator = { "(", ", ", ") : (", ")", ":" };
            foreach (var l in lines)
            {
                string[] c = l.Split(spearator, StringSplitOptions.RemoveEmptyEntries);
                //Debug.Log($"length {c.Length}");
                //Debug.Log($"lines  : {c[0]} {c[1]} {c[2]} {c[3]} {c[4]}");
                imgPts[i] = new Point2f(float.Parse(c[0]), float.Parse(c[1]));
                objPts[i] = new Point3f(float.Parse(c[2]), float.Parse(c[3]), float.Parse(c[4]));
                i++;
            }
            //synth.PointAndPnP(imgPts, objPts);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (generate)
        {

            if (method == Methods.manual)
            {
                if (frameCount == 0)
                {
                    synth.Save(new Vector3[0], cadObj.GetComponent<Collider>().bounds, "image_groundtruth", width, height, "captures/GroundTruth", true, true);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    Vector3 mFar = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.farClipPlane);
                    Vector3 mNear = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane);

                    Vector3 mousePosF = Camera.main.ScreenToWorldPoint(mFar);
                    Vector3 mousePosN = Camera.main.ScreenToWorldPoint(mNear);

                    Debug.DrawRay(mousePosN, mousePosF - mousePosN, Color.green);

                    RaycastHit hit;

                    if (Physics.Raycast(mousePosN, mousePosF - mousePosN, out hit))
                    {
                        //Debug.Log($"hit.point {hit.point}  WorldToScreenPoint(hit.point) {Camera.main.WorldToScreenPoint(hit.point)} hit.transform.position {hit.transform.position} object.position {hit.point - hit.transform.position}");
                        Debug.Log($"Point3D {hit.point}  :  Point2D {Input.mousePosition} ");
                        tw.WriteLine("(" + Input.mousePosition.x + ", " + Input.mousePosition.y + ") : " + (hit.point - hit.transform.position));
                    }
                    tw.Flush();
                }
            }
            else if (method == Methods.orb)
            {
                if (frameCount == 0)
                {
                    synth.Save(new Vector3[0], cadObj.GetComponent<Collider>().bounds, "image_groundtruth", width, height, "captures/GroundTruth", true, false);
                }
            }
        }
        else
        {
            string folder = "captures/";
            string filename = $"image_{frameCount.ToString().PadLeft(5, '0')}";

            if (control)
            {
                if (frameCount < trainingImages)
                {
                    folder = folder + "Train";
                    createAndSave(folder, filename);
                }
            }
            else if (convert)
            {
                if (frameCount < testImages)
                {
                    folder = folder + "Test";
                    createAndSave(folder, filename);
                }
            }
        }
        frameCount++;
    }

    private void createAndSave(string folder, string filename)
    {
        generate3DObjects();
        Vector3[] transformedObjPoints = transformCADObject();
        Bounds objBound = cadObj.GetComponent<Collider>().bounds;

        //Debug.Log($" Parameters : {folder} {filename} {control}");
        synth.Save(transformedObjPoints, objBound, filename, width, height, folder, true, control);
    }

    private Vector3[] transformCADObject()
    {
        // position
        float newX, newY, newZ;
        newX = UnityEngine.Random.Range(-5.0f, 5.0f);
        newY = UnityEngine.Random.Range(0.0f, -5.0f);
        newZ = UnityEngine.Random.Range(0.0f, 5.0f);

        Vector3 newPosition = new Vector3(newX, newY, newZ);
        // Rotation
        var newRot = Quaternion.Euler(0, UnityEngine.Random.Range(-180, 180), 0);
        //Debug.Log($"initPos  {initPos}");
        cadObj.transform.position = initPos - newPosition;
        cadObj.transform.rotation = newRot * initRot;

        Vector3[] transformedObjPoints = new Vector3[kpLen];
        int i = 0;
        foreach (var p3D in objPts)
        {
            //Vector3 obj = newRot * (new Vector3(p3D.X, p3D.Y, p3D.Z) - cadObj.transform.position) + cadObj.transform.position;
            Vector3 obj = new Vector3(p3D.X, p3D.Y, p3D.Z);
            // Apply same rotation to points as object
            obj = newRot * obj;
            // Apply same transaltion to points as object
            obj = obj + initPos - newPosition;
            //Debug.Log($"obj after {obj}");
            transformedObjPoints[i] = obj;
            i++;
        }
        Debug.Log($"PoBJECT TRANSFORME ");
        synth.OnSceneChange();
        return transformedObjPoints;
    }

    void generate3DObjects()
    {
        pool.reclaimAll();
        int objectCount = UnityEngine.Random.Range(count.minObject, count.maxObject);
        // pick random prefab
        for (int i = 0; i < objectCount; i++)
        {
            int prefabIndex = UnityEngine.Random.Range(0, prefabs.Length);

            // position
            float newX, newY, newZ;
            newX = UnityEngine.Random.Range(-30.0f, 30.0f);
            newY = UnityEngine.Random.Range(2.0f, 10.0f);
            newZ = UnityEngine.Random.Range(-30.0f, 30.0f);

            Vector3 newPosition = new Vector3(newX, newY, newZ);
            // Rotation
            var newRot = UnityEngine.Random.rotation;
            // Instantiate new Object from the pool.
            var shape = pool.get(prefabIndex);
            var newObj = shape.obj;

            newObj.transform.position = newPosition;
            newObj.transform.rotation = newRot;

            //scale
            float sx = UnityEngine.Random.Range(5f, 10.0f);
            Vector3 newScale = new Vector3(sx, sx, sx);

            newObj.transform.localScale = newScale;

            // color
            float newRed, newBlue, newGreen;

            newRed = UnityEngine.Random.Range(0.0f, 1.0f);
            newBlue = UnityEngine.Random.Range(0.0f, 1.0f);
            newGreen = UnityEngine.Random.Range(0.0f, 1.0f);

            var newColor = new Color(newRed, newBlue, newGreen);
        }
        synth.OnSceneChange();
    }

    private void decideAction()
    {
        if (Actions.none == action)
        {
            Debug.Break();
            throw new Exception("Action can not be None");
        }
        else if (Actions.generate == action)
        {
            generate = true;
        }
        else if (Actions.control == action)
        {
            control = true;
        }
        else if (Actions.convert == action)
        {
            convert = true;
        }
    }

    private void decideMethod()
    {
        if (Actions.generate == action && method == Methods.none)
        {
            Debug.Break();
            throw new Exception("If action in Generate then method should not be none");
        }
        else if (Actions.generate != action && method != Methods.none)
        {
            Debug.Break();
            throw new Exception("Method should only be set with Action Generated");
        }
    }
}
