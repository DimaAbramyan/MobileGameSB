using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
public class PlayerController : MonoBehaviour
    {
        private ParentShip currentShip;
        public ParentShip CurrentShip => currentShip;

        public event System.Action<ParentShip> OnCurrentShipChanged;

        [SerializeField] 
        private Rigidbody2D playerRB; 
        float speed;
        Vector3 _currentSpeed;
        Vector3 _currentPosition;
        void Awake()
        {
            playerRB = GetComponent<Rigidbody2D>();
        }
        private void FixedUpdate()
        {
            _currentPosition = gameObject.transform.position;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (IsPointerOverUIObject(touch))
                {
                    return;
                }
                Vector3 touchPosition = Camera.main.ScreenToWorldPoint(Input.touches[i].position);
                touchPosition = new Vector2 (touchPosition.x, touchPosition.y);
                if ((touchPosition - _currentPosition).magnitude < 0.25f)
                {
                    _currentSpeed = (touchPosition - _currentPosition) * speed;
                }
                else
                {
                    _currentSpeed = (touchPosition - _currentPosition).normalized * speed;
                }
                //Debug.Log((touchPosition - _currentPosition).normalized);
                //Debug.Log((touchPosition));
                playerRB.AddForce(_currentSpeed);
            }
        }
        private bool IsPointerOverUIObject(Touch touch)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = touch.position
            };
            System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }
        /// <summary>
        /// Изменяет физическое значение массы, трение, а также скорость текущего корабля
        /// </summary>
        /// <param name="currShip"></param>
        public void ChangeShipData(ParentShip currShip)
        {
            currentShip = currShip;
            Debug.Log($"Смена корабля на {currentShip.ShipData.shipId}");

            playerRB.mass = currShip.ShipData.mass;
            playerRB.drag = currShip.ShipData.drag;
            speed =         currShip.ShipData.speed;

            OnCurrentShipChanged?.Invoke(currentShip);
        }
    }