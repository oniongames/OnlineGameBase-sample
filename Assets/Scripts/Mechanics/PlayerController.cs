using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        public JumpState jumpState = JumpState.Grounded;
        private bool stopJump;
        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;
        [NonSerialized]
        public bool useExternalInput;

        bool jump;
        Vector2 move;
        float externalHorizontal;
        bool externalJumpPressed;
        bool externalJumpReleased;
        bool snapshotPlayback;
        bool snapshotPlaybackImmediate;
        Vector2 snapshotPlaybackTarget;
        Vector2 snapshotPlaybackVelocity;
        JumpState snapshotPlaybackJumpState;
        bool snapshotPlaybackControlEnabled;
        bool snapshotPlaybackFacingLeft;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;

        public Bounds Bounds => collider2d.bounds;
        public bool FacingLeft => spriteRenderer != null && spriteRenderer.flipX;

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            
            m_MoveAction.Enable();
            m_JumpAction.Enable();
        }

        protected override void Update()
        {
            if (snapshotPlayback)
            {
                UpdateSnapshotPlayback();
                return;
            }

            if (controlEnabled)
            {
                if (useExternalInput)
                {
                    move.x = externalHorizontal;
                    if (jumpState == JumpState.Grounded && externalJumpPressed)
                        jumpState = JumpState.PrepareToJump;
                    else if (externalJumpReleased)
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }

                    externalJumpPressed = false;
                    externalJumpReleased = false;
                }
                else
                {
                    move.x = m_MoveAction.ReadValue<Vector2>().x;
                    if (jumpState == JumpState.Grounded && m_JumpAction.WasPressedThisFrame())
                        jumpState = JumpState.PrepareToJump;
                    else if (m_JumpAction.WasReleasedThisFrame())
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }
                }
            }
            else
            {
                move.x = 0;
            }
            UpdateJumpState();
            base.Update();
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (move.x > 0.01f)
                spriteRenderer.flipX = false;
            else if (move.x < -0.01f)
                spriteRenderer.flipX = true;

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);

            targetVelocity = move * maxSpeed;
        }

        public void SetExternalInput(float horizontal, bool jumpPressed, bool jumpReleased)
        {
            externalHorizontal = Mathf.Clamp(horizontal, -1f, 1f);
            externalJumpPressed |= jumpPressed;
            externalJumpReleased |= jumpReleased;
        }

        public void ClearExternalInput()
        {
            externalHorizontal = 0f;
            externalJumpPressed = false;
            externalJumpReleased = false;
        }

        public void SetSnapshotPlayback(bool enabled)
        {
            if (snapshotPlayback == enabled)
                return;

            snapshotPlayback = enabled;
            suspendKinematicSimulation = enabled;
            snapshotPlaybackImmediate = enabled;

            if (!enabled)
                ClearExternalInput();
        }

        public void ApplySnapshotPlaybackFrame(Vector2 position, Vector2 replayVelocity, int replayJumpState, bool replayControlEnabled, bool replayFacingLeft)
        {
            if (!snapshotPlayback)
                SetSnapshotPlayback(true);

            snapshotPlaybackTarget = position;
            snapshotPlaybackVelocity = replayVelocity;
            snapshotPlaybackJumpState = ClampJumpState(replayJumpState);
            snapshotPlaybackControlEnabled = replayControlEnabled;
            snapshotPlaybackFacingLeft = replayFacingLeft;
        }

        private void UpdateSnapshotPlayback()
        {
            ClearExternalInput();

            var current = (Vector2)transform.position;
            var blend = snapshotPlaybackImmediate ? 1f : 1f - Mathf.Exp(-24f * Time.deltaTime);
            var next = Vector2.Lerp(current, snapshotPlaybackTarget, blend);

            Teleport(new Vector3(next.x, next.y, transform.position.z));
            velocity = snapshotPlaybackVelocity;
            jumpState = snapshotPlaybackJumpState;
            controlEnabled = snapshotPlaybackControlEnabled;
            snapshotPlaybackImmediate = false;

            if (animator != null)
            {
                animator.SetBool("grounded", jumpState == JumpState.Grounded || jumpState == JumpState.Landed);
                animator.SetFloat("velocityX", Mathf.Abs(snapshotPlaybackVelocity.x) / maxSpeed);
            }

            if (spriteRenderer != null)
                spriteRenderer.flipX = snapshotPlaybackFacingLeft;
        }

        private static JumpState ClampJumpState(int replayJumpState)
        {
            if (replayJumpState < 0 || replayJumpState > (int)JumpState.Landed)
                return JumpState.Grounded;

            return (JumpState)replayJumpState;
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}
