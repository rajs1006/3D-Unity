using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Runtime.InteropServices;

public class Example : MonoBehaviour
{

    //public ImageSynthesis synth;

    public Boolean first =  false;
    private TextWriter tw;

    private double[, ] cameraMatrix;
    private Mat projMatrix;

    public GameObject g;
    Point3f[] objPts;

    private int frameCount = 0;
    
    private ORB orb;

    // Start is called before the first frame update
    void Start()
    {
    
        Debug.Log("projection : " + Camera.main.projectionMatrix);

        var f = Camera.main.focalLength;
        var sensorSize = Camera.main.sensorSize;

        var sx = sensorSize.x;
        var sy =  sensorSize.y;
        var width = Camera.main.pixelWidth;
        var height = Camera.main.pixelHeight;

        Debug.Log(" f " + f + "  sensor " +  sensorSize + " : "+sx+ " width/height " + width + "  :  " + height);

        cameraMatrix = new double[3, 3]
        {
            { (width*f)/sx, 0, width/2 },
            { 0, (height*f)/sy, height/2 },
            { 0, 0, 1}
        };
        
        if(first){
            tw = new StreamWriter("2d-3d-file.txt");
        }
        else{
            tw = new StreamWriter("2d-3d-file-kp.txt");

            Point2f[] imgPts = new Point2f[21];
            objPts = new Point3f[21];
            int i = 0;

            string[] lines = File.ReadAllLines("2d-3d-file_refined.txt");
            String[] spearator = { "(",", ",") (",")" }; 
            foreach(var l  in lines){
                String[] c = l.Split(spearator, StringSplitOptions.RemoveEmptyEntries);
                
                //Debug.Log($"lines  : {c[0]} {c[1]} {c[2]} {c[3]} {c[4]} {c[5]}");
                imgPts[i] = new Point2f(float.Parse(c[0]), float.Parse(c[1]));
                objPts[i] = new Point3f(float.Parse(c[3]), float.Parse(c[4]), float.Parse(c[5]));
                i++;
            }

            pnp(objPts, imgPts, cameraMatrix, SolvePnPFlags.Iterative);
        }
    }


    void Update()
    {

        if(first){

            if(Input.GetMouseButtonDown(0)){

                Vector3  mFar = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.farClipPlane);
                Vector3  mNear = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane);

                Vector3 mousePosF = Camera.main.ScreenToWorldPoint(mFar);
                Vector3 mousePosN = Camera.main.ScreenToWorldPoint(mNear);

                Debug.DrawRay(mousePosN, mousePosF-mousePosN, Color.green);

                RaycastHit hit;
                
                if(Physics.Raycast(mousePosN,  mousePosF-mousePosN, out hit)) {
                    Debug.Log($"hit.point {hit.point}  WorldToScreenPoint(hit.point) {Camera.main.WorldToScreenPoint(hit.point)}");
                    Debug.Log($"Point3D {hit.point}  :  Point2D {Input.mousePosition} ");

                    var initRot = g.transform.rotation;

                    Vector3 v =  new Vector3(1,0,0);
                    var rot = Quaternion.Euler(0, 45, 0);

                    Debug.Log($"Init sample Rotated Point3D {rot * initRot * v}  :  Point2D {Camera.main.WorldToScreenPoint(rot * initRot * v)} ");
                    Debug.Log($"sample Rotated Point3D {rot * v}  :  Point2D {Camera.main.WorldToScreenPoint(rot * v)} ");
                
                    g.transform.rotation = rot * initRot;
                    var rotP = rot * hit.point;
                    Debug.Log($"Rotated Point3D {rotP}  :  Point2D {Camera.main.WorldToScreenPoint(rotP)} ");

                    PointAndPnP(Input.mousePosition, hit.point);
                    Vector3 pnpPoints = project3DPoints(hit.point);

                    tw.WriteLine(Input.mousePosition + " : " + hit.point + " : " + pnpPoints);
                }
                tw.Flush();
            }
        }
        else{
            frameCount++;
           
            if (frameCount < 20)
            {

                float newX, newY, newZ;
                newX = UnityEngine.Random.Range(0.0f, 10.0f);
                newY = UnityEngine.Random.Range(1.0f, 1.5f);
                newZ = UnityEngine.Random.Range(0.0f, 10.0f);

                Vector3 newPosition = new Vector3(newX, newY, newZ);

                var rot = UnityEngine.Random.Range(-180, 180);

                g.transform.position = newPosition;
                g.transform.rotation = Quaternion.Euler(0, rot, 0);

                foreach (var p3D in objPts){

                    Vector3 obj = new Vector3(p3D.X + newX, p3D.Y + newY, p3D.Z + newZ); 

                    obj  = Quaternion.Euler(0, rot, 0) * obj; 

                    Vector3 pnpPoints = project3DPoints(obj);
                    tw.WriteLine(pnpPoints +" " + obj);
                }
                tw.Flush();

                String image = $"image_{frameCount.ToString().PadLeft(5, '0')}";
                // var mesharray = null;
                // synth.Save(mesharray, image, 512, 512, "captures/Test", true);
            }
            
        }
            
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

    Vector3 project3DPoints(Vector3 obj){

        Mat point2d_vec = new Mat(4, 1, MatType.CV_64FC1);

        Mat cameraMatrixMat = new Mat(3, 3, MatType.CV_64FC1, cameraMatrix);

        Mat point3d_vec = new Mat(4, 1, MatType.CV_64FC1);
        point3d_vec.Set<double>(0, obj.x);
        point3d_vec.Set<double>(1, obj.y);
        point3d_vec.Set<double>(2, obj.z);
        point3d_vec.Set<double>(3, 1);

        point2d_vec = cameraMatrixMat * projMatrix * point3d_vec;

        var X2D = point2d_vec.At<double>(0)/point2d_vec.At<double>(2);
        var Y2D = point2d_vec.At<double>(1)/point2d_vec.At<double>(2);

        Debug.Log(" 3d Points " + obj.x + " " + obj.y + " " + obj.z);
        Debug.Log(" 2d point  x : " + X2D + " y " + Y2D);
        //Debug.Log(" float 2d point  x : " + (float)X2D + " y " + (float)Y2D);

        Vector3 pnpPoints = new Vector3((float)X2D, (float)Y2D, 0.0f);

        return pnpPoints;
    }

    void PointAndPnP(Vector3 points2D, Vector3 points3D){

        var imgPts = new []
        {
            new Point2f(points2D.x, points2D.y),
            new Point2f(points2D.x, points2D.y),
            new Point2f(points2D.x, points2D.y),
            new Point2f(points2D.x, points2D.y),
            new Point2f(points2D.x, points2D.y),
            new Point2f(points2D.x, points2D.y)
        };

        var objPts = new []
        {
            new Point3f(points3D.x, points3D.y, points3D.z),
            new Point3f(points3D.x, points3D.y, points3D.z),
            new Point3f(points3D.x, points3D.y, points3D.z),
            new Point3f(points3D.x, points3D.y, points3D.z),
            new Point3f(points3D.x, points3D.y, points3D.z),
            new Point3f(points3D.x, points3D.y, points3D.z)
        };

        pnp(objPts, imgPts, cameraMatrix, SolvePnPFlags.Iterative);
    }


    void pnp(Point3f[] objPoints, Point2f[] imgPts,double[, ] cameraMatrix, SolvePnPFlags type){

        
        using (var objPtsMat = new Mat(objPoints.Length, 1, MatType.CV_32FC3, objPoints))
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
 
            projMatrix = proj_matrix;
            
            // Debug.Log(" rvecMat.Height  " +rvecMat.Height);
            // for (int y = 0; y < rvecMat.Height; y++){
            //     Debug.Log(" r-double : "+rvecMat.At<double>(y));
            //     Debug.Log(" r-float : "+(float)rvecMat.At<double>(y));

            //     Debug.Log(" t-double : "+tvecMat.At<double>(y));
            //     Debug.Log(" t-float : "+(float)tvecMat.At<double>(y));
            // }

            // Debug.Log(" Quaternion.LookRotation(relativePos, Vector3.up) " + Camera.main.transform.position);
            // Debug.Log(" transform.rotation " + Camera.main.transform.eulerAngles);

            // Debug.Log(" Rotation matrix FLOAT : " 
            //         + new Vector3(rvecMat.Get<float>(0), rvecMat.Get<float>(1), rvecMat.Get<float>(2)));
            // Debug.Log(" ScreenToWorldPoint() for Rotation matrix FLOAT : " 
            //         + Camera.main.ScreenToWorldPoint(new Vector3(rvecMat.Get<float>(0), rvecMat.Get<float>(1), rvecMat.Get<float>(2))));

            // Debug.Log(" Rotation matrix DOUBLE : " 
            //         + new Vector3((float)rvecMat.Get<double>(0), (float)rvecMat.Get<double>(1), (float)rvecMat.Get<double>(2)));
            // Debug.Log(" ScreenToWorldPoint() for Rotation matrix DOUBLE : " 
            //         + Camera.main.ScreenToWorldPoint(new Vector3((float)rvecMat.Get<double>(0), (float)rvecMat.Get<double>(1), (float)rvecMat.Get<double>(2))));

            // Debug.Log(" Translation matrix FLOAT : " 
            //         + new Vector3(tvecMat.Get<float>(0), tvecMat.Get<float>(1), tvecMat.Get<float>(2)));
            // Debug.Log(" ScreenToWorldPoint() for Translation matrix FLOAT : " 
            //         + Camera.main.ScreenToWorldPoint(new Vector3(tvecMat.Get<float>(0), tvecMat.Get<float>(1), tvecMat.Get<float>(2))));

            // Debug.Log(" Translation matrix DOUBLE : " 
            //         + new Vector3((float)tvecMat.Get<double>(0), (float)tvecMat.Get<double>(1), (float)tvecMat.Get<double>(2)));
            // Debug.Log(" ScreenToWorldPoint() for Translation matrix DOUBLE : " 
            //         + Camera.main.ScreenToWorldPoint(new Vector3((float)tvecMat.Get<double>(0), (float)tvecMat.Get<double>(1), (float)tvecMat.Get<double>(2))));


            // Vector3 m = new Vector3 ((float)rvecMat.Get<double>(0), (float)rvecMat.Get<double>(1), (float)rvecMat.Get<double>(2));
            //Vector3 m = new Vector3 (rvecMat.Get<float>(0), rvecMat.Get<float>(1), rvecMat.Get<float>(2));
            // float theta = (float)(Math.Sqrt(rvecMat.Get<double>(0)*rvecMat.Get<double>(0) 
            //     + rvecMat.Get<double>(1)*rvecMat.Get<double>(1)
            //     + rvecMat.Get<double>(2)*rvecMat.Get<double>(2))*180/Math.PI);

            // Vector3 axis = Camera.main.ScreenToWorldPoint(new Vector3 (-(float)rvecMat.Get<double>(0), (float)rvecMat.Get<double>(1), -(float)rvecMat.Get<double>(2)));

            // float theta = (float)(Math.Sqrt(m.x*m.x + m.y*m.y + m.z*m.z)*180/Math.PI);
            // Vector3 axis = new Vector3 (m.x, -m.y, m.z);
            // Quaternion rot = Quaternion.AngleAxis(theta, axis);
            
            // Quaternion rot = Quaternion.LookRotation(new Vector3(-(float)rot_matrix.At<double>(0,2), (float)rot_matrix.At<double>(1,2), -(float)rot_matrix.At<double>(2,2))
            //     , new Vector3(-(float)rot_matrix.At<double>(0,1), (float)rot_matrix.At<double>(1,1), -(float)rot_matrix.At<double>(2,1)));
            
            // // Quaternion rot = Quaternion.LookRotation(new Vector3(rot_matrix.At<float>(0,2), -rot_matrix.At<float>(1,2), rot_matrix.At<float>(2,2))
            // //     , new Vector3(rot_matrix.At<float>(0,1), -rot_matrix.At<float>(1,1), rot_matrix.At<float>(2,1)));

            // Debug.Log(" Rotation " + rot.eulerAngles);

            // double sy = Math.Sqrt(rot_matrix.At<double>(2,1) * rot_matrix.At<double>(2,1) +  rot_matrix.At<double>(2,2) * rot_matrix.At<double>(2,2) );
            // var x = Math.Atan2(rot_matrix.At<double>(1,0) , rot_matrix.At<double>(0,0));
            // var y = Math.Atan2(-rot_matrix.At<double>(2,0), sy);
            // var z = Math.Atan2(rot_matrix.At<double>(2,1), rot_matrix.At<double>(2,2));

            // double theta = Math.Acos((rot_matrix.At<double>(0,0) + rot_matrix.At<double>(1,1) + rot_matrix.At<double>(2,2) - 1)/2);
            // var x = (rot_matrix.At<double>(2,1) - rot_matrix.At<double>(1,2))/(2 *  Math.Sin(theta));
            // var y = (rot_matrix.At<double>(0,2) - rot_matrix.At<double>(2,0))/(2 *  Math.Sin(theta));
            // var z = (rot_matrix.At<double>(1,0) - rot_matrix.At<double>(0,1))/(2 *  Math.Sin(theta));

            // Debug.Log(" Rotation screen : "+ x + " : "+y+" : "+ z);
            // Debug.Log(" Rotation screen : "+ new Vector3((float)x,(float)y,(float)z));
            // Debug.Log(" Rotation : "+ Camera.main.ScreenToWorldPoint(new Vector3((float)x,(float)y,(float)z)));

            // Mat cameraMatrix1 = new Mat();
            // Mat rotMatrix =  new Mat();
            // Mat transVect = new Mat();
            // Mat rotMatrixX = new Mat();
            // Mat rotMatrixY = new Mat();
            // Mat rotMatrixZ = new Mat();
            // Mat eulerAngles = new Mat();

            // Cv2.DecomposeProjectionMatrix(proj_matrix,
            //                    cameraMatrix1,
            //                    rotMatrix,
            //                    transVect,
            //                    rotMatrixX,
            //                    rotMatrixY,
            //                    rotMatrixZ,
            //                    eulerAngles);
                               
            // Debug.Log(" Camera matrix "+ cameraMatrix[0, 0] + " : "+ cameraMatrix[0, 1] +" : "+ cameraMatrix[0, 2] );
            // Debug.Log(" cameraMatrix1 "+cameraMatrix1.At<double>(0, 0) + " : "+cameraMatrix1.At<double>(0,1)+ " : "+cameraMatrix1.At<double>(0,2) );
            // Debug.Log(" eulerAngles "+eulerAngles.At<double>(0) + " : "+eulerAngles.At<double>(1)+ " : "+eulerAngles.At<double>(2) );

            //Debug.Log($"Projection Matrix   {proj_matrix}");

            // foreach (var p3D in objPoints){
            
            //     Mat point3d_vec = new Mat(4, 1, MatType.CV_64FC1);
            //     point3d_vec.Set<double>(0, p3D.X);
            //     point3d_vec.Set<double>(1, p3D.Y);
            //     point3d_vec.Set<double>(2, p3D.Z);
            //     point3d_vec.Set<double>(3, 1);


            //     Mat point2d_vec = new Mat(4, 1, MatType.CV_64FC1);
            //     point2d_vec = cameraMatrixMat * proj_matrix * point3d_vec;
            //     //Debug.Log(" 3d Points " + p3D.X + " " + p3D.Y + " " + p3D.Z);
            //     //Debug.Log(" 2d point  x : " + point2d_vec.At<double>(0)/point2d_vec.At<double>(2) + " y " + point2d_vec.At<double>(1)/point2d_vec.At<double>(2));
            // }
        }
    }
}
