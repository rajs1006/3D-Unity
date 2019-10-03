using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundColorControler : MonoBehaviour
{
    public Material[] materials;
    public string backgroundMaterialName;
    public string textureMaterialName;
    public Dictionary<string, Material> m;
    public Texture[] textures;
    public SampleCountController count;
    private int trainingImages;
    private int testImages;
    private Renderer rend;

    private int frameCount = 0;
    // Start is called before the first frame update
    void Start()
    {
        //Fetch the Renderer from the GameObject
        trainingImages = count.trainingImages;
        testImages = count.testImages;

        rend = GetComponent<Renderer>();
        rend.enabled = true;
        //rend.material = materials[0];
    }

    // Update is called once per frame
    void Update()
    {
        if (frameCount < trainingImages + testImages)
        {
            // foreach (var mat in materials)
            // {
                //if (frameCount % 2 == 0 && string.Compare(mat.name, backgroundMaterialName) == 0)
                //{
                    //rend.material = mat;
                    //Set the main Color of the Material to random
                    rend.material.SetColor("_Color", new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)));
                    rend.material.SetFloat("_Metallic", Random.Range(0.2f, 1.0f));
                    rend.material.SetFloat("_Glossiness", Random.Range(0.2f, 1.0f));
                    //Debug.Log($"Changin background color");
                //}
                //else if (frameCount % 2 != 0 && string.Compare(mat.name, textureMaterialName) == 0)
                //{
                //    Material textureMaterial = mat;
                 //   textureMaterial.mainTexture = textures[Random.Range(0, textures.Length)];
                 //   rend.material = textureMaterial;

                //}
           // }
            frameCount++;
        }
    }
}
