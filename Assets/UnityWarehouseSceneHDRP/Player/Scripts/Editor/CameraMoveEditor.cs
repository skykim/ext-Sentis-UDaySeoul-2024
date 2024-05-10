using UnityEditor;





namespace UnityWarehouseSceneHDRP
{
	[CustomEditor(typeof(CameraMove))]
	public class CameraMoveEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUILayout.Space(10);
			EditorGUILayout.HelpBox("Move : WASD key\nEye Level : QE key\nRotate : Right drag\nRapid movement : Shift key\nWalk/Fly : F key", MessageType.Info);
		}
	}
}