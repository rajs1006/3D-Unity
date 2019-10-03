using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Shape
{
    public int label;
    public GameObject obj;
}

public class ShapePool : ScriptableObject
{
    private GameObject[] preFabs;
    private Dictionary<int, List<Shape>> pools;
    private List<Shape> active;

    public static ShapePool create(GameObject[] preFabs)
    {
        var p = ScriptableObject.CreateInstance<ShapePool>();
        p.preFabs = preFabs;
        p.pools = new Dictionary<int, List<Shape>>();

        for (int i = 0; i < preFabs.Length; i++)
        {
            Debug.Log($"Shapelabel : {i}");
            p.pools[i] = new List<Shape>();
        }

        p.active = new List<Shape>();
        return p;
    }

    public Shape get(int label)
    {
        var pool = pools[label];
        int lastIndex = pool.Count - 1;
        Shape retShape;

        if (lastIndex <= 0)
        {
            var obj = Instantiate(preFabs[(int)label]);
            retShape = new Shape() { label = label, obj = obj };
        }
        else
        {
            retShape = pool[lastIndex];
            retShape.obj.SetActive(true);
            pool.RemoveAt(lastIndex);
        }
        active.Add(retShape);
        return retShape;
    }


    public void reclaimAll()
    {
        foreach (var shape in active)
        {
            shape.obj.SetActive(false);
            pools[shape.label].Add(shape);
        }
        active.Clear();
    }

}
