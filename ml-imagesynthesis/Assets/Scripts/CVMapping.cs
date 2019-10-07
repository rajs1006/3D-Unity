﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using OpenCvSharp;
using System.Runtime.InteropServices;

public class CVMapping : MonoBehaviour
{
    // Start is called before the first frame update

    private ORB orb;
    private Mat projectionMatrix;

    void Awake()
    {
        DontDestroyOnLoad(this);
    }


    public KeyPoint[] getKeyPoints(Mat camMat, int nKeyPoints)
    {
        
        orb = ORB.Create(nKeyPoints);
        KeyPoint[] keyPoints = orb.Detect(camMat);

        return keyPoints;        
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

    public double[,] getCameraMatrix(Camera cam)
    {

        var f = cam.focalLength;
        var sensorSize = cam.sensorSize;

        var sx = sensorSize.x;
        var sy = sensorSize.y;
        var width = cam.pixelWidth;
        var height = cam.pixelHeight;

        //Debug.Log(" f " + f + "  sensor " + sensorSize + " : " + sx + " width/height " + width + "  :  " + height);

        double[,] cameraMatrix = new double[3, 3]
        {
            { (width*f)/sx, 0, width/2 },
            { 0, (height*f)/sy, height/2 },
            { 0, 0, 1}
        };

        return cameraMatrix;
    }

    public Vector3 project3DPoints(Vector3 obj, Camera cam)
    {

        Mat point2d_vec = new Mat(4, 1, MatType.CV_64FC1);

        Mat cameraMatrixMat = new Mat(3, 3, MatType.CV_64FC1, getCameraMatrix(cam));

        Mat point3d_vec = new Mat(4, 1, MatType.CV_64FC1);
        point3d_vec.Set<double>(0, obj.x);
        point3d_vec.Set<double>(1, obj.y);
        point3d_vec.Set<double>(2, obj.z);
        point3d_vec.Set<double>(3, 1);

        Debug.Log($"projectionMatrix  : {projectionMatrix.Get<double>(0, 0)} {projectionMatrix.Get<double>(0, 1)} {projectionMatrix.Get<double>(0, 2)}");
        point2d_vec = cameraMatrixMat * projectionMatrix * point3d_vec;

        var X2D = point2d_vec.At<double>(0) / point2d_vec.At<double>(2);
        var Y2D = point2d_vec.At<double>(1) / point2d_vec.At<double>(2);

        // Debug.Log(" 3d Points " + obj.x + " " + obj.y + " " + obj.z);
        // Debug.Log(" 2d point  x : " + X2D + " y " + Y2D);
        //Debug.Log(" float 2d point  x : " + (float)X2D + " y " + (float)Y2D);

        Vector3 pnpPoints = new Vector3((float)X2D, (float)Y2D, 0.0f);

        return pnpPoints;
    }

    public void pnp(Point3f[] objPts, Point2f[] imgPts, Camera cam, SolvePnPFlags type)
    {

        Debug.Log($" PNP   : {objPts.Length} : {imgPts.Length}");
        using (var objPtsMat = new Mat(objPts.Length, 1, MatType.CV_32FC3, objPts))
        using (var imgPtsMat = new Mat(imgPts.Length, 1, MatType.CV_32FC2, imgPts))
        using (var cameraMatrixMat = new Mat(3, 3, MatType.CV_64FC1, getCameraMatrix(cam)))
        using (var distMat = Mat.Zeros(5, 0, MatType.CV_64FC1))
        using (var rvecMat = new Mat())
        using (var tvecMat = new Mat())
        {

            Cv2.SolvePnP(objPtsMat, imgPtsMat, cameraMatrixMat, distMat, rvecMat, tvecMat, flags: type);
            //Debug.Log("Solved " + rvecMat + "  :  "+tvecMat);

            Mat rot_matrix = Mat.Zeros(3, 3, MatType.CV_64FC1);
            Mat tr_matrix = Mat.Zeros(3, 1, MatType.CV_64FC1);
            Mat proj_matrix = Mat.Zeros(3, 4, MatType.CV_64FC1);

            Cv2.Rodrigues(rvecMat, rot_matrix);
            tr_matrix = tvecMat;

            proj_matrix.Set<double>(0, 0, rot_matrix.At<double>(0, 0));
            proj_matrix.Set<double>(0, 1, rot_matrix.At<double>(0, 1));
            proj_matrix.Set<double>(0, 2, rot_matrix.At<double>(0, 2));

            proj_matrix.Set<double>(1, 0, rot_matrix.At<double>(1, 0));
            proj_matrix.Set<double>(1, 1, rot_matrix.At<double>(1, 1));
            proj_matrix.Set<double>(1, 2, rot_matrix.At<double>(1, 2));

            proj_matrix.Set<double>(2, 0, rot_matrix.At<double>(2, 0));
            proj_matrix.Set<double>(2, 1, rot_matrix.At<double>(2, 1));
            proj_matrix.Set<double>(2, 2, rot_matrix.At<double>(2, 2));

            proj_matrix.Set<double>(0, 3, tr_matrix.At<double>(0));
            proj_matrix.Set<double>(1, 3, tr_matrix.At<double>(1));
            proj_matrix.Set<double>(2, 3, tr_matrix.At<double>(2));

            projectionMatrix = proj_matrix;
        }
    }

    public Vector2[] screenScaleOfObject(Bounds objBound, Camera cam)
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
        foreach (Vector2 v in extentPoints)
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
