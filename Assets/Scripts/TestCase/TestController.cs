using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class TestController : MonoBehaviour
{
    [Header("Player Input")]
    
    [SerializeField] PlayerInput playerInput;
    private InputAction movementAction;
    private InputAction jumpAction;
    [SerializeField]private PlayerControlInput _playerControlInput;

    [Header("Player Physic")]
    [SerializeField] Rigidbody2D myRigidBody2D;

    [Header("Player Animation")]
    [SerializeField] Animator animator;

    [Header("Movement")]
    [SerializeField] Vector3 rawMovement;
    [SerializeField] Vector3 velocity;
    [SerializeField] Vector3 lastPosition;
    [SerializeField,Range(0,20),Tooltip("Raising this value increases collision accuracy at the cost of performance.")] int freeCollisionIteration = 5;
    private float currentVerticalMovement, currentHorizontalMovement;

    [Header("Walk")]
    [SerializeField] float acceleration=90;
    [SerializeField] float deacceleration=60;
    [SerializeField] float moveClamp =13;
    [SerializeField] float apexBonus=2;

    [Header("Jump")]
    [SerializeField] float apexPoint;
    [SerializeField] float jumpHeight=30;
    [SerializeField] float jumpApexThreshold =10;
    [SerializeField] float jumpBuffer =0.1f;
    [SerializeField] float jumpEndEarlyModifier=3;
    [SerializeField] float lastJumpPressed;
    [SerializeField] bool endJumpEarly = true;
 
    bool HadBufferTime => colDown && lastJumpPressed + jumpBuffer > Time.time;


    [Header("Gravity")]
    [SerializeField] float fallClamp = -40f;
    [SerializeField] float minFallSpeed = 80f;
    [SerializeField] float maxFallSpeed = 120f;
    float fallSpeed =1;
    

    [Header("Player Collision")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask wallLayer;
    [SerializeField] private Bounds _characterBounds;
    [SerializeField] bool colUp, colDown ,colLeft , colRight ;
    [SerializeField] bool colWallLeft, colWallRight;
    [SerializeField, Range(0.1f, 0.3f)] float rayBuffer =0.1f;
    [SerializeField, Range(1,6)] int detectorNumber = 3;
    [SerializeField, Range(0.1f,0.6f)] float detectorlength = 0.1f;
    [SerializeField] float timeLeftGrounded;
    RayRange rayUp, rayDown, rayLeft, rayRight;

    [Header("Coyote time")]
    [SerializeField] float coyeteTimeThreshold = 0.1f;
    [SerializeField] bool coyeteTimeUsable;
    [SerializeField] bool LandingThisFrame;
    [SerializeField] bool JumpingThisFrame;
    bool CanUseCoyoteTime => coyeteTimeUsable && !colDown && timeLeftGrounded + coyeteTimeThreshold > Time.time;

    [Header("Game State")]
    [SerializeField] bool GameActive;

    #region Input Region


    private void Awake()
    {
        
       
        movementAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        Invoke(nameof(ActivateGame), 0.5f);
    }

    void ActivateGame()
    {
        GameActive= true;
    }
    private void OnEnable()
    {
        movementAction.Enable();
        jumpAction.Enable();
        jumpAction.started += OnJump;
        jumpAction.performed += OnJump;
        jumpAction.canceled += OnJump;
        movementAction.started += OnRun;
        movementAction.performed += OnRun;
        movementAction.canceled += OnRun;
    }

    private void OnJump(InputAction.CallbackContext CallBack)
    {
        
    }

    private void OnRun(InputAction.CallbackContext CallBack)
    {
        _playerControlInput.X = CallBack.ReadValue<Vector2>().x;

    }

  

    #endregion

    
    private void Update()
    {
        if (!GameActive)
        {
            return;
        }
        velocity = (transform.position - lastPosition)/Time.deltaTime;
        lastPosition= transform.position;

        GatherJumpTime();
        HandleCollision();
        CalculateWalk();
        CalculateJumpApex();
        CalculateGravity();
        CalcuateJump();    
        MoveCharacter();
     
    }

    private void GatherJumpTime()
    {


        _playerControlInput.JumpDown = jumpAction.IsPressed();
        _playerControlInput.JumpUp = jumpAction.WasReleasedThisFrame();


        if (_playerControlInput.JumpDown)
        {
            lastJumpPressed = Time.time;
        }
    }
    #region Collision
    private void HandleCollision()
    {
        GetRayRange();
        LandingThisFrame = false;
        var groundCheck = RunDetection(rayDown);
        if (colDown&&!groundCheck)
        {
            timeLeftGrounded = Time.time;
        }
        else if (!colUp&&groundCheck)
        {
            coyeteTimeUsable = true;
            LandingThisFrame= true;
        }

        colDown = groundCheck;
        colRight = RunDetection (rayRight);
        colLeft= RunDetection(rayLeft);
        colUp= RunDetection(rayUp);
        colWallLeft = RunDetectionWall(rayLeft);
        colWallRight= RunDetectionWall(rayRight);
        bool RunDetection(RayRange range) => EvaluteCollisionPosition(range).Any(point => Physics2D.Raycast(point, range.Dir, detectorlength, groundLayer));
        bool RunDetectionWall(RayRange range) => EvaluteCollisionPosition(range).Any(point => Physics2D.Raycast(point, range.Dir, detectorlength, wallLayer));
          
    }

    void GetRayRange()
    {
        var tempBound = new Bounds(transform.position+ _characterBounds.center, _characterBounds.size);
        rayUp = new RayRange(tempBound.min.x + rayBuffer, tempBound.max.y, tempBound.max.x - rayBuffer, tempBound.max.y, Vector2.up);
        rayDown = new RayRange(tempBound.min.x + rayBuffer, tempBound.min.y, tempBound.max.x - rayBuffer, tempBound.min.y, Vector2.down);
        rayLeft = new RayRange(tempBound.min.x,tempBound.min.y + rayBuffer, tempBound.min.x , tempBound.max.y - rayBuffer, Vector2.left);
        rayRight = new RayRange(tempBound.max.x,tempBound.min.y + rayBuffer, tempBound.max.x , tempBound.max.y - rayBuffer, Vector2.right);
    }

    IEnumerable<Vector2> EvaluteCollisionPosition(RayRange range)
    {
        for (var i = 0; i < detectorNumber ; i++)
        {
            var time = (float)i / (detectorNumber - 1);
            yield return Vector2.Lerp(range.Start, range.End, time);
        }
    }
    #endregion

    #region Gravity

    void CalculateGravity()
    {

        if (colWallLeft || colWallRight)
        {
            if (currentVerticalMovement > 0 || currentVerticalMovement < 0)
            {
                currentVerticalMovement = -0.5f;
            }
        }
        else
        {
            if (colDown)
            {
                if (currentVerticalMovement < 0)
                {
                    currentVerticalMovement = 0;
                }

            }

            else
            {

                var _fallSpeed = endJumpEarly && currentVerticalMovement > 0 ? fallSpeed * jumpEndEarlyModifier : fallSpeed;
                currentVerticalMovement -= _fallSpeed * Time.deltaTime;

                if (currentVerticalMovement < fallClamp)
                {
                    currentVerticalMovement = fallClamp;
                }
            }
        }
        
    }
    #endregion

    #region Jump
    void CalculateJumpApex()
    {
        if (!colDown)
        {
            apexPoint = Mathf.InverseLerp(jumpApexThreshold,0,Mathf.Abs(velocity.y));
            fallSpeed = Mathf.Lerp(minFallSpeed,maxFallSpeed,apexPoint);
        }
        else
        {
            apexPoint = 0;
        }
    }

    void CalcuateJump()
    {
        if (_playerControlInput.JumpDown && CanUseCoyoteTime || HadBufferTime  )
        {
            Debug.Log("Jumping");
            Jumpers();
        }
        else
        {
            JumpingThisFrame= false;
        }

        if (!colDown&&_playerControlInput.JumpUp&&!endJumpEarly&& velocity.y>0)
        {
            Debug.Log("JumpUp");
            endJumpEarly = true;
        }

        if (colUp)
        {
            if (currentVerticalMovement > 0) 
            {
                currentVerticalMovement = 0;
            }
        }

        if (_playerControlInput.JumpDown && colWallLeft || _playerControlInput.JumpDown && colWallRight)
        {
            Jumpers();
        }
    }

    private void Jumpers()
    {
        currentVerticalMovement = jumpHeight;
        endJumpEarly = false;
        coyeteTimeUsable = false;
        timeLeftGrounded = float.MinValue;
        JumpingThisFrame = true;
    }
    #endregion

    #region Movement

    void MoveCharacter()
    {
       
        var position = transform.position + _characterBounds.center;
        rawMovement = new Vector3(currentHorizontalMovement, currentVerticalMovement);
        var move = rawMovement*Time.deltaTime;
        var futherestPoint = position + move;

        var hitInfo = Physics2D.OverlapBox(futherestPoint,_characterBounds.size,0,groundLayer);
        if (!hitInfo)
        {
            transform.position += move;
            return;
        }

        var positionToMove = transform.position;
        for (int i = 1; i < freeCollisionIteration; i++)
        {
            var time = (float)i / (freeCollisionIteration);
            var positionToTry = Vector2.Lerp(position, futherestPoint, time);

            if (Physics2D.OverlapBox(positionToTry, _characterBounds.size, 0, groundLayer))
            {
                transform.position = positionToMove;
                if (i==1)
                {
                    if (currentVerticalMovement < 0){currentVerticalMovement = 0;}
                    var direction = transform.position - hitInfo.transform.position;
                    transform.position += direction.normalized * move.magnitude;
                }
                return;
            }
            positionToMove = positionToTry;
        }
    }

    void CalculateWalk()
    {
        if (movementAction.IsInProgress())
        {
            currentHorizontalMovement += _playerControlInput.X * acceleration * Time.deltaTime;

            currentHorizontalMovement =Mathf.Clamp(currentHorizontalMovement,-moveClamp,moveClamp);

            var _apexBonus = Mathf.Sign(_playerControlInput.X) * apexBonus * apexPoint;
            currentHorizontalMovement += _apexBonus * Time.deltaTime;
        }
        else
        {
            currentHorizontalMovement = Mathf.MoveTowards(currentHorizontalMovement,0, deacceleration* Time.deltaTime);
        }

        if (currentHorizontalMovement > 0 && colRight || currentHorizontalMovement < 0 && colLeft)
        {
            currentHorizontalMovement = 0;
        }
        if (currentHorizontalMovement > 0 && colWallRight || currentHorizontalMovement < 0 && colWallLeft)
        {
            currentHorizontalMovement = 0;
        }
    }

    #endregion

    #region Gizmo
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + _characterBounds.center, _characterBounds.size);

        GetRayRange();
        Gizmos.color = Color.red;
        foreach (var range in new List<RayRange>{ rayUp, rayDown, rayRight,rayLeft })
        {
            foreach (var point in EvaluteCollisionPosition(range))
            {
                Gizmos.DrawRay(point, range.Dir * detectorlength);
            }
        }

    }

    #endregion

    #region Scene End
    private void OnDisable()
    {
        movementAction.Disable();
        jumpAction.Disable();
        jumpAction.started -= OnJump;
        jumpAction.performed -= OnJump;
        jumpAction.canceled -= OnJump;
        movementAction.started -= OnRun;
        movementAction.performed -= OnRun;
        movementAction.canceled -= OnRun;
    }
    #endregion


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("FallDetector"))
        {
            Debug.Log("HIt");
        }
    }
}

[Serializable]
public struct PlayerControlInput
{
    public float X;
    public bool JumpUp;
    public bool JumpDown;
   
}

[Serializable]
public struct RayRange
{
    public RayRange(float x1, float y1, float x2, float y2, Vector2 dir)
    {
        Start = new Vector2(x1, y1);
        End = new Vector2(x2, y2);
        Dir = dir;
    }

    public readonly Vector2 Start, End, Dir;
}