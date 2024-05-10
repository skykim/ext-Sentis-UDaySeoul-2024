using UnityEngine;





namespace UnityWarehouseSceneHDRP
{
    public class Label : MonoBehaviour
    {
	    private void Start()
	    {
            int[] array = { 0, 1, 2, 3 };
            System.Random rand = new System.Random();

            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                int temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }

            Renderer renderer = GetComponent<Renderer>();
		    renderer.material.SetVector("_Label_Offset_1", new Vector2(Random.Range(-0.1f, -0.9f) - array[0] * 2, Random.Range(-0.25f, -0.75f)));
            renderer.material.SetVector("_Label_Offset_2", new Vector2(Random.Range(-0.1f, -0.9f) - array[1] * 2, Random.Range(-0.25f, -0.75f)));
            renderer.material.SetVector("_Label_Offset_3", new Vector2(Random.Range(-0.1f, -0.9f) - array[2] * 2, Random.Range(-0.25f, -0.75f)));
            renderer.material.SetVector("_Label_Offset_4", new Vector2(Random.Range(-0.1f, -0.9f) - array[3] * 2, Random.Range(-0.25f, -0.75f)));
	    }
    }
}