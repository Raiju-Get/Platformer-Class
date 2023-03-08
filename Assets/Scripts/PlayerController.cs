using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{

    [Header("Movement")]
    [SerializeField] Vector2 moveInput;
    [SerializeField] float acceleration;
    [SerializeField] float topSpeed;
    [SerializeField] float decelerationTime;
    [SerializeField] bool isPressed;
    float tempVelocityX;
    float tempVelocity;
   

    [Header("Jump Movement")]
    [SerializeField] float jumpForce;
    [SerializeField] float maxJumpForce;
    [SerializeField] float fallMultiplier;
    [SerializeField] float HighJumpMultiplier;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundRadius;
    [SerializeField] Transform checkPoint;
    [SerializeField] bool isGrounded;
    float tempGravity;

    [Header("Input System")]
    [SerializeField] PlayerInput playerInput;
    InputAction movementAction;
    InputAction jumpAction;

    [Header("Physic System")]
    [SerializeField] Rigidbody2D myBody;

  
  



    private void Awake()
    {
        movementAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        tempGravity = myBody.gravityScale;
    }

  

    private void OnEnable()
    {
        movementAction.Enable();
        jumpAction.Enable();
        jumpAction.started += JumpAction_started;
        jumpAction.performed += JumpAction_performed;
        jumpAction.canceled += JumpAction_canceled;
        movementAction.started += MovementAction_started;
        movementAction.performed += MovementAction_performed;
        movementAction.canceled += MovementAction_canceled;
    }



   
    private void OnDisable()
    {
        movementAction.Disable();
        jumpAction.Disable();
        jumpAction.started -= JumpAction_started;
        jumpAction.performed -= JumpAction_performed;
        jumpAction.canceled -= JumpAction_canceled;
        movementAction.started -= MovementAction_started;
        movementAction.performed -= MovementAction_performed;
        movementAction.canceled -= MovementAction_canceled;
       
    }
    private void Start()
    {
        
    }

    private void FixedUpdate()
    {
        PlayerMovement();
        ModifyJumpPhysic();

    }

    private void ModifyJumpPhysic()
    {
        isGrounded = Physics2D.OverlapCircle(checkPoint.position, groundRadius, groundLayer);
        if (!isGrounded)
        {
            if (myBody.velocity.y < 0)
            {
                myBody.gravityScale = fallMultiplier;
            }
            else if (myBody.velocity.y>0 && !jumpAction.IsInProgress())
            {
                myBody.gravityScale = HighJumpMultiplier;
           
            }
            else
            {
                myBody.gravityScale = tempGravity;

            }

            myBody.velocity = new Vector2(myBody.velocity.x, Mathf.Clamp(myBody.velocity.y, -maxJumpForce*2, maxJumpForce));
        }
        else
        {
            myBody.gravityScale = 1;

        }

    }

    private void PlayerMovement()
    {
        if (isPressed)
        {
            moveInput = movementAction.ReadValue<Vector2>();
            myBody.AddForce(new Vector2(moveInput.x * acceleration * Time.deltaTime, 0), ForceMode2D.Impulse);
            myBody.velocity = new Vector2(Mathf.Clamp(myBody.velocity.x, -topSpeed, topSpeed), myBody.velocity.y);
            transform.rotation = moveInput.x > 0 ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);
        }
        else
        {
            if (Mathf.Abs(myBody.velocity.x) > Mathf.Epsilon)
            {
                tempVelocityX = Mathf.SmoothDamp(myBody.velocity.x, 0, ref tempVelocity, decelerationTime);
                myBody.velocity = new Vector2(tempVelocityX, myBody.velocity.y);

            }
        }


    }

    private void MovementAction_started(InputAction.CallbackContext callback)
    {
        isPressed= true;
    }
    private void MovementAction_performed(InputAction.CallbackContext callbackContext)
    {
      
      
    }
    private void MovementAction_canceled(InputAction.CallbackContext callback)
    {
        isPressed = false;

    }

    private void JumpAction_performed(InputAction.CallbackContext callback)
    {
       
      
        if (isGrounded)
        {
            if (callback.action.IsPressed())
            {
                JumpCharacter();
            }

           
            
        }
        Debug.Log("Started");

    }
    private void JumpAction_started(InputAction.CallbackContext callback)
    {
     
    }
    private void JumpAction_canceled(InputAction.CallbackContext callback)
    {
   
    }




    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(checkPoint.transform.position, groundRadius);
    }


    private void JumpCharacter()
    {
        myBody.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("FallDetector"))
        {
            Destroy(gameObject);
        }
    }
}
