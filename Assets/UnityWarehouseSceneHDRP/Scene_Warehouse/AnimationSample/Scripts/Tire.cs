using System.Collections;
using System.Collections.Generic;
using UnityEngine;





namespace UnityWarehouseSceneHDRP
{
    public class Tire : MonoBehaviour
    {
        public Transform root;
        public Transform[] wheels;
        public float speed;


        private Vector3 prevPos;


        private void Start()
        {
            prevPos = root.position;
        }


        private void Update()
        {
            float delta = Vector3.Distance(prevPos, root.position);
            int forward = 1;
            if(Vector3.Distance(prevPos, root.position + root.forward * 0.1f) < Vector3.Distance(root.position, root.position + root.forward * 0.1f))
            {
                forward = -1;
            }

            foreach(Transform wheel in wheels)
            {
                wheel.Rotate(new Vector3(delta * forward * speed, 0, 0), Space.Self);
            }

            prevPos = root.position;
        }
    }
}