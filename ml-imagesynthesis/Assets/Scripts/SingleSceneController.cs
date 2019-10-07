using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class SingleSceneController : MonoBehaviour
{
    public ImageSynthesis synth;

    public GameObject cadObj;
    public SampleCountController count;
    public bool first = false;
    public bool control = false;


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

    // Start is called before the first frame update
    void Start()
    {

        var height = Camera.main.pixelHeight;
        var width = Camera.main.pixelWidth;
        Debug.Log("Height : " + height + "  Width : " + width);

        trainingImages = count.trainingImages;
        testImages = count.testImages;

        initPos = cadObj.transform.position;
        initRot = cadObj.transform.rotation;

        if (first)
        {
            tw = new StreamWriter(gtKpFile);
        }
        else
        {
            string[] lines = File.ReadAllLines(gtKpFile);

            kpLen = lines.Length;
            int i = 0;

            imgPts = new Point2f[kpLen];
            objPts = new Point3f[kpLen];

            string[] spearator = { "(", ", ", ") : (", ")", ":" };
            foreach (var l in lines)
            {
                string[] c = l.Split(spearator, StringSplitOptions.RemoveEmptyEntries);

                //Debug.Log($"lines  : {c[0]} {c[1]} {c[2]} {c[3]} {c[4]} {c[5]}");
                imgPts[i] = new Point2f(float.Parse(c[0]), float.Parse(c[1]));
                objPts[i] = new Point3f(float.Parse(c[3]), float.Parse(c[4]), float.Parse(c[5]));
                i++;
            }
            synth.PointAndPnP(imgPts, objPts);

        }
    }

    // Update is called once per frame
    void Update()
    {
        if (first)
        {

            if (frameCount == 0)
            {
                synth.Save(new Vector3[0], cadObj.GetComponent<Collider>().bounds, "image_groundtruth", width, height, "captures/GroundTruth", true);
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

                    // GameObject tile1 = hit.collider.gameObject;
                    // tile1.GetComponent<Outline>().enabled = true; 

                    tw.WriteLine(Input.mousePosition + " : " + (hit.point - hit.transform.position));
                }
                tw.Flush();
            }
        }
        else
        {

            string folder = "captures/";
            string filename = $"image_{frameCount.ToString().PadLeft(5, '0')}";

            Vector3[] transformedObjPoints = transformCADObject();
            Bounds objBound = cadObj.GetComponent<Collider>().bounds;

            if (control)
            {
                if (frameCount < trainingImages)
                {
                    folder = folder + "Train";
                    //Debug.Log($" Parameters : {folder} {filename} {control}");
                    synth.Save(transformedObjPoints, objBound, filename, width, height, folder, true, control);
                }
                else
                {
                    // Pause the application after completion of data generation.
                    Debug.Break();
                    //synth.stopExecution();
                }
            }
            else
            {
                if (frameCount < testImages)
                {
                    folder = folder + "Test";
                    //Debug.Log($" Parameters : {folder} {filename} {control}");
                    synth.Save(transformedObjPoints, objBound, filename, width, height, folder, true, control);
                }
                else
                {
                    Debug.Log(" CLosinf");
                    // Pause the application after completion of data generation.
                    Debug.Break();
                    //synth.stopExecution();
                }
            }
        }
        frameCount++;
    }

    private Vector3[] transformCADObject()
    {
        // position
        float newX, newY, newZ;
        newX = UnityEngine.Random.Range(-10.0f, 10.0f);
        newY = UnityEngine.Random.Range(0.0f, -10.0f);
        newZ = UnityEngine.Random.Range(0.0f, 10.0f);

        Vector3 newPosition = new Vector3(newX, newY, newZ);
        // Rotation
        var newRot = Quaternion.Euler(0, UnityEngine.Random.Range(-180, 180), 0);

        cadObj.transform.position = initPos - newPosition;
        //cadObj.transform.rotation = newRot * initRot;

        Vector3[] transformedObjPoints = new Vector3[kpLen];
        int i = 0;
        foreach (var p3D in objPts)
        {
            Vector3 obj = new Vector3(p3D.X - newX, p3D.Y - newY, p3D.Z - newZ);
            //obj  = newRot * obj; 

            transformedObjPoints[i] = obj;
            i++;
        }

        synth.OnSceneChange();
        return transformedObjPoints;
    }

}
