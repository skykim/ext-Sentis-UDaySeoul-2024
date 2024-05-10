using UnityEngine;





namespace UnityWarehouseSceneHDRP
{
	public class CameraMove : MonoBehaviour
	{
		[SerializeField] private CharacterController _characterController;
		[SerializeField] private Transform _playerRoot;
		[SerializeField] private Transform _camera;

		[Space(10)]
		[SerializeField] private float _moveSpeed = 2;
		[SerializeField] private float _rotateSpeed = 2;

		[Space(10)]
		[SerializeField] private float _minWorldY;


		private float _yaw = 0;
		private float _tilt = 0;
		private bool _isRunning = false;
		private bool _isWalkMode = true;




		private void Awake()
		{
			_yaw = _playerRoot.eulerAngles.y;
			_tilt = _camera.localEulerAngles.x;
		}



		private void Update()
		{
			// Rotate
			if(Input.GetMouseButton(1))
			{
				_yaw  += Input.GetAxis("Mouse X") * _rotateSpeed;
				_tilt -= Input.GetAxis("Mouse Y") * _rotateSpeed;

				_tilt = Mathf.Clamp(_tilt, -89, 89);

				_playerRoot.eulerAngles = new Vector3(0, _yaw, 0);
				_camera.localEulerAngles = new Vector3(_tilt, 0, 0);
			}

			// Move
			Vector3 dir = new Vector3(Input.GetAxis("Horizontal"), 0 , Input.GetAxis("Vertical"));
			float height = Mathf.Max(0, _camera.localPosition.y + ((Input.GetKey(KeyCode.Q) ? -_moveSpeed : 0) + (Input.GetKey(KeyCode.E) ? _moveSpeed : 0)) * Time.deltaTime);

			if(Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
			{
				_isRunning = !_isRunning;
			}

			if(_isWalkMode)
			{
				dir = Quaternion.Euler(0, _playerRoot.localEulerAngles.y, 0) * dir;
				_characterController.SimpleMove(dir * _moveSpeed * (_isRunning ? 3 : 1));
				_camera.localPosition = new Vector3(0, height, 0);
			}
			else
			{
				dir = Quaternion.Euler(_camera.localEulerAngles.x, _playerRoot.localEulerAngles.y, _camera.localEulerAngles.z) * dir;
				_characterController.Move(dir * _moveSpeed * (_isRunning ? 3 : 1) * Time.deltaTime);
			}

			if(_playerRoot.position.y < _minWorldY)
			{
				Vector3 position = _playerRoot.position;
				position.y = _minWorldY;
				_playerRoot.position = position;
			}

			// Change mode
			if(Input.GetKeyDown(KeyCode.F))
			{
				_isWalkMode = !_isWalkMode;
				if(_isWalkMode)
				{
					_playerRoot.position = new Vector3(_playerRoot.position.x, _minWorldY, _playerRoot.position.z);
					_camera.localPosition = new Vector3(0, 1.5f, 0);
				}
				else
				{
					_playerRoot.position = new Vector3(_playerRoot.position.x, _camera.position.y, _playerRoot.position.z);
					_camera.localPosition = Vector3.zero;
				}
			}
		}
	}
}