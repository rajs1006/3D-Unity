using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Importer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ObjImporter newMesh = new ObjImporter();
        Mesh holderMesh = newMesh.ImportFile("/home/sourabh/Documents/TU-Berlin/Thesis/Sytheticdata/ml-imagesynthesis/Assets/CADPictures/PandaMale.obj");

        MeshSaver.SaveMesh(holderMesh, "PandaMale");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
