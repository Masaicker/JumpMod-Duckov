using ECM2;
using Jump;
using UnityEngine;
using UnityEngine.InputSystem;
using Duckov.Modding;
using DG.Tweening;
using ModBehaviour = Jump.ModBehaviour;
using FMOD.Studio;

/// <summary>
/// 角色跳跃控制器 - 马里奥式跳跃
/// 混合控制：初始力度 + 持续微调
/// 支持ModSetting配置系统
/// </summary>
public class CharacterJumpController : MonoBehaviour
{
    private CharacterMainControl characterMainControl = null!;
    private Movement movement = null!;
    private CharacterMovement characterMovement = null!;

    // 配置参数 - 从JumpConfigManager获取
    private static JumpConfigManager? configManager;

    // 跳跃状态
    public static bool isJumping = false;
    private bool isJumpingHeld = false;
    private bool jumpReleased = false; // 标记跳跃键是否已释放
    private float currentJumpPower = 0f;
    private float currentBoostAcceleration = 0f;
    private float jumpStartHeight = 0f;

    // 空中水平控制管理
    private Vector3 airHorizontalVelocity = Vector3.zero;     // 当前空中水平速度
    private Vector3 initialInheritedVelocity = Vector3.zero; // 继承的初始惯性

    // Scale动效相关参数
    private Vector3 jumpStartScale;    // 跳跃开始时的原始scale
    private Tween? jumpScaleTween;      // DOTween动画
    private float currentStartScaleY;   // 记录当前实际的Y轴scale（用于下落动画）

    // 音效管理
    private EventInstance? jumpSoundInstance;

    // 可配置的Scale动画参数
    [Header("跳跃Scale动画参数")]
    [SerializeField] private float jumpSquashFactor = 0.8f;        // 起跳挤压系数 (0.5 = 压缩到50%)
    [SerializeField] private float fallStretchFactor = 1.3f;       // 下落拉伸系数 (1.2 = 拉伸到120%)
    [SerializeField] private float landingBounceFactor = 0.85f;    // 着陆挤压系数 (0.85 = 压缩到85%)

    [Header("动画时间参数")]
    [SerializeField] private float squashDuration = 0.08f;         // 挤压动画时间
    [SerializeField] private float bounceMinDuration = 0.3f;       // 回弹最小时间
    [SerializeField] private float bounceMaxDuration = 0.5f;       // 回弹最大时间

    
    // 输入缓存 - 落地前的跳跃输入记录
    private bool hasJumpBufferedInput = false;
    private float jumpBufferCounter = 0f;
    private const float JUMP_BUFFER_TIME = 0.2f;

    // 土狼机制 - 离地后短时间内仍可跳跃
    private bool wasOnGroundLastFrame = false;
    private float coyoteTimeCounter = 0f;
    private const float COYOTE_TIME = 0.15f;  // 150ms土狼时间

    void Start()
    {
        characterMainControl = GetComponent<CharacterMainControl>();
        if (characterMainControl != null)
        {
            movement = characterMainControl.movementControl;
            characterMovement = movement.GetComponent<CharacterMovement>();
        }
        else
        {
            JumpLogger.LogRed("找不到CharacterMainControl组件！");
            enabled = false;
            return;
        }

        if (characterMovement == null)
        {
            JumpLogger.LogRed("找不到CharacterMovement组件！");
            enabled = false;
        }

        // 获取配置管理器
        configManager = ModBehaviour.GetConfigManager();
    }

    void Update()
    {
        if (!LevelManager.LevelInited || characterMainControl.Health.IsDead)
            return;

        HandleJumpInput();
        UpdateJumpState();
    }

    /// <summary>
    /// 处理跳跃输入
    /// </summary>
    private void HandleJumpInput()
    {
        if (configManager == null) return;

        // 检查配置的跳跃按键
        KeyCode jumpKey = configManager.JumpKey;

        // 将KeyCode转换为InputSystem的Key
        Key jumpInputKey = (Key)System.Enum.Parse(typeof(Key), jumpKey.ToString());

        // 按下跳跃键
        if (Keyboard.current[jumpInputKey].wasPressedThisFrame)
        {
            bool jumpStarted = TryStartJump();

            // 只有跳跃完全没有开始时才设置输入缓存
            if (!jumpStarted && CanBufferJumpInput())
            {
                JumpLogger.LogYellow($"设置输入缓存: {JUMP_BUFFER_TIME}秒");
                hasJumpBufferedInput = true;
                jumpBufferCounter = JUMP_BUFFER_TIME;
            }
        }

        // 松开跳跃键
        if (Keyboard.current[jumpInputKey].wasReleasedThisFrame)
        {
            ReleaseJump();
        }

        // 更新按住状态 - 只有在跳跃键未释放的情况下才更新
        if (!jumpReleased)
        {
            isJumpingHeld = Keyboard.current[jumpInputKey].isPressed;
        }
    }

    /// <summary>
    /// 尝试开始跳跃
    /// </summary>
    private bool TryStartJump()
    {
        if (CanJump())
        {
            StartJump();
            return true; // 跳跃成功开始
        }
        return false; // 跳跃失败
    }

    /// <summary>
    /// 检查是否可以跳跃
    /// </summary>
    private bool CanJump()
    {
        if (isJumping)
        {
            return false;
        }

        // 支持地面状态或土狼时间内跳跃
        bool canJumpByGround = movement.IsOnGround;
        bool canJumpByCoyote = coyoteTimeCounter > 0f;

        if (!canJumpByGround && !canJumpByCoyote)
        {
            return false;
        }

        if (!characterMainControl.CanMove())
        {
            return false;
        }

        if (characterMainControl.CurrentAction != null &&
            characterMainControl.CurrentAction.Running &&
            !characterMainControl.CurrentAction.CanMove())
        {
            return false;
        }

        // 检查输入管理器状态 - 输入被禁用时禁止跳跃
        if (!InputManager.InputActived)
        {
            JumpLogger.LogYellow("输入被禁用，禁止跳跃");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 检查是否可以缓存跳跃输入
    /// </summary>
    private bool CanBufferJumpInput()
    {
        if (characterMainControl.Health.IsDead || !characterMainControl.CanMove())
        {
            JumpLogger.LogYellow("状态不允许，不设置跳跃缓存");
            return false;
        }

        // 检查输入管理器状态 - 输入被禁用时禁止缓存跳跃输入
        if (!InputManager.InputActived)
        {
            JumpLogger.LogYellow("输入被禁用，不设置跳跃缓存");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 开始跳跃 - 混合控制核心
    /// </summary>
    private void StartJump()
    {
        if (configManager == null) return;

        isJumping = true;
        isJumpingHeld = true;
        currentJumpPower = configManager.MinJumpPower;
        currentBoostAcceleration = configManager.BoostAcceleration;
        jumpStartHeight = transform.position.y; // 记录起跳点高度

        // 清除输入缓存状态
        hasJumpBufferedInput = false;
        jumpBufferCounter = 0f;

        // 获取当前的地面水平速度作为惯性基础
        Vector3 currentVelocity = characterMovement.velocity;
        initialInheritedVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        airHorizontalVelocity = initialInheritedVelocity; // 初始化空中水平速度

        // 设置跳跃初始速度，使用继承的水平惯性
        Vector3 jumpVelocity = new Vector3(airHorizontalVelocity.x, currentJumpPower, airHorizontalVelocity.z);
        characterMovement.velocity = jumpVelocity;

        JumpLogger.LogWhite($"跳跃开始 - 继承水平惯性: X={airHorizontalVelocity.x:F2}, Z={airHorizontalVelocity.z:F2}");

        // 暂停地面约束
        characterMovement.PauseGroundConstraint();

        // DOTween挤压动画
        jumpStartScale = Vector3.one; // 记录原始scale

        // 杀死任何现有的Scale动画
        jumpScaleTween?.Kill();

        // 起跳挤压：瞬间压缩然后快速恢复
        Vector3 squashedScale = new Vector3(jumpStartScale.x, jumpStartScale.y * jumpSquashFactor, jumpStartScale.z);
        jumpScaleTween = transform.DOScale(squashedScale, squashDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(StartStretchAnimation);

        // 播放跳跃音效
        PlayJumpSound();

        JumpLogger.LogWhite($"跳跃开始 - 初始力度: {currentJumpPower:F2}");
        JumpLogger.LogWhite($"DOTween挤压动画 - 目标ScaleY: {squashedScale.y:F3}");
    }

    /// <summary>
    /// 开始拉伸动画 - 使用Sequence控制两阶段动画
    /// </summary>
    private void StartStretchAnimation()
    {
        if (!isJumping) return;

        jumpScaleTween?.Kill();
        jumpScaleTween = DOTween.Sequence()
            .Append(transform.DOScale(new Vector3(jumpStartScale.x, jumpStartScale.y * 1.15f, jumpStartScale.z), 0.15f)
                .SetEase(Ease.OutQuad))
            .Append(transform.DOScale(jumpStartScale, 0.15f)
                .SetEase(Ease.InQuad))
            .OnComplete(() => JumpLogger.LogWhite("拉伸动画Sequence完成"));
        JumpLogger.LogWhite("开始拉伸动画Sequence - 0.2秒拉伸到1.2倍，0.1秒还原到1倍");
    }

    
    
    /// <summary>
    /// 着陆回弹动画 - 渐进过渡效果，可被打断
    /// </summary>
    private void StartLandingBounceAnimation()
    {
        // 根据跳跃高度动态调整回弹参数
        float jumpHeight = transform.position.y - jumpStartHeight;
        if (configManager == null) return;
        float maxExpectedHeight = configManager.MaxJumpPower * configManager.MaxJumpPower / (2f * Mathf.Abs(Physics.gravity.y));
        float heightRatio = Mathf.Clamp01(jumpHeight / maxExpectedHeight);

        // 回弹时间：跳得越高，回弹时间越长
        float bounceDuration = bounceMinDuration + heightRatio * (bounceMaxDuration - bounceMinDuration);

        // 先快速挤压到着陆状态
        Vector3 squashedScale = new Vector3(jumpStartScale.x, jumpStartScale.y * landingBounceFactor, jumpStartScale.z);
        float squashTime = bounceDuration * 0.2f; // 挤压占用20%的时间

        jumpScaleTween?.Kill();
        jumpScaleTween = transform.DOScale(squashedScale, squashTime)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                // 挤压完成后开始回弹
                float bounceTime = bounceDuration * 0.8f; // 回弹占用80%的时间
                jumpScaleTween = transform.DOScale(jumpStartScale, bounceTime)
                    .SetEase(Ease.OutElastic, 1f, 0.8f + heightRatio * 0.4f); // 弹性因子也根据高度调整

                JumpLogger.LogWhite($"DOTween着陆回弹 - 开始回弹，时间: {bounceTime:F3}s");
            });

        JumpLogger.LogWhite($"DOTween着陆回弹 - 挤压ScaleY: {squashedScale.y:F3}, 挤压时间: {squashTime:F3}s, 总时间: {bounceDuration:F3}s");
    }

    /// <summary>
    /// 松开跳跃键 - 停止加速
    /// </summary>
    private void ReleaseJump()
    {
        if (!isJumping)
            return;

        isJumpingHeld = false;
        jumpReleased = true; // 标记跳跃键已释放，不再接受加速输入
        JumpLogger.LogWhite($"跳跃结束 - 最终力度: {currentJumpPower:F2}");
    }

    /// <summary>
    /// 更新跳跃状态
    /// </summary>
    private void UpdateJumpState()
    {
        if (configManager == null) return;

        // 更新输入缓存计时器
        UpdateTimers();

        // 持续加速逻辑 - 物理衰减模式
        if (isJumping && isJumpingHeld && !jumpReleased && characterMovement.velocity.y > 0 && currentJumpPower < configManager.MaxJumpPower)
        {
            // 应用当前加速度
            Vector3 velocity = characterMovement.velocity;
            velocity.y += currentBoostAcceleration * Time.deltaTime;
            velocity.y = Mathf.Min(velocity.y, configManager.MaxJumpPower);
            characterMovement.velocity = velocity;

            currentJumpPower = velocity.y;

            // 加速度自然衰减
            currentBoostAcceleration *= Mathf.Pow(configManager.AccelerationDecay, Time.deltaTime);

            // 如果加速度太小了，就停止加速
            if (currentBoostAcceleration < 0.1f)
            {
                currentBoostAcceleration = 0f;
            }

            //JumpLogger.LogWhite($"持续加速 - 当前力度: {currentJumpPower:F2}, 当前加速度: {currentBoostAcceleration:F2}, 跳跃高度: {transform.position.y - jumpStartHeight:F2}m, 垂直速度: {characterMovement.velocity.y:F2}");
        }

        
        // 实时下落动画 - 直接根据速度动态计算Scale
        if (isJumping && characterMovement.velocity.y < 0)
        {
            if (jumpScaleTween != null)
            {
                jumpScaleTween.Kill();
                jumpScaleTween = null;
                currentStartScaleY = transform.localScale.y;
            }
            // 实时计算下落拉伸效果
            float fallSpeed = Mathf.Abs(characterMovement.velocity.y);
            float maxFallSpeed = Mathf.Abs(Physics.gravity.y) * 0.8f; // 基于重力加速度，更合理的基准
            float fallRatio = Mathf.Clamp01(fallSpeed / maxFallSpeed);

            // 下落时Y轴拉伸：从当前Y轴开始拉伸，避免瞬间还原
            float reducedStretchFactor = 1.0f + (fallStretchFactor - 1.0f);
            float smoothFallRatio = fallRatio * fallRatio; // 平方缓动，先慢后快
            float stretchFactor = 1.0f + smoothFallRatio * (reducedStretchFactor - 1.0f);

            Vector3 targetFallScale = new Vector3(jumpStartScale.x, currentStartScaleY * stretchFactor, jumpStartScale.z);
            transform.localScale = targetFallScale;
        }

        // 空中水平控制系统
        if (isJumping)
        {
            UpdateAirControl();
        }
        // 检测着陆 - 检测接触地面
        if (isJumping && movement.IsOnGround && characterMovement.velocity.y <= 0f)
        {
            Land();
        }
        // 如果不在跳跃中，但有输入缓存且刚接触地面，也执行跳跃
        else if (!isJumping && movement.IsOnGround && hasJumpBufferedInput && jumpBufferCounter > 0f)
        {
            JumpLogger.LogYellow("检测到地面且有输入缓存，直接执行跳跃");
            float bufferTime = jumpBufferCounter;
            hasJumpBufferedInput = false;
            jumpBufferCounter = 0f;

            if (CanJump())
            {
                StartJump();
                JumpLogger.LogYellow($"输入缓存成功执行，剩余缓存时间: {bufferTime:F3}秒");
            }
        }
    }

    /// <summary>
    /// 更新输入缓存计时器
    /// </summary>
    private void UpdateTimers()
    {
        // 更新输入缓存
        if (hasJumpBufferedInput && jumpBufferCounter > 0f)
        {
            //float oldBuffer = jumpBufferCounter;
            jumpBufferCounter -= Time.deltaTime;
            //JumpLogger.LogYellow($"输入缓存倒计时: {oldBuffer:F3} -> {jumpBufferCounter:F3}");

            if (jumpBufferCounter <= 0f)
            {
                JumpLogger.LogYellow("输入缓存超时，清除缓存");
                hasJumpBufferedInput = false;
                jumpBufferCounter = 0f;
            }
        }

        // 更新土狼时间
        UpdateCoyoteTime();
    }

    /// <summary>
    /// 更新土狼机制 - 检测离地状态
    /// </summary>
    private void UpdateCoyoteTime()
    {
        bool currentlyOnGround = movement.IsOnGround;

        // 检测离地瞬间：只有非跳跃状态下离开地面才启动土狼时间
        if (wasOnGroundLastFrame && !currentlyOnGround && !isJumping)
        {
            coyoteTimeCounter = COYOTE_TIME;
            JumpLogger.LogWhite($"土狼时间启动: {COYOTE_TIME}秒");
        }

        // 更新土狼计时器
        if (coyoteTimeCounter > 0f)
        {
            coyoteTimeCounter -= Time.deltaTime;
            if (coyoteTimeCounter <= 0f)
            {
                JumpLogger.LogWhite("土狼时间结束");
                coyoteTimeCounter = 0f;
            }
        }

        wasOnGroundLastFrame = currentlyOnGround;
    }

    /// <summary>
    /// 着陆处理
    /// </summary>
    private void Land()
    {
        float finalHeight = transform.position.y - jumpStartHeight;
        JumpLogger.LogWhite($"着陆 - 最终跳跃力度: {currentJumpPower:F2}, 跳跃高度: {finalHeight:F2}m");

        isJumping = false;
        isJumpingHeld = false;
        jumpReleased = false; // 重置释放状态
        currentJumpPower = 0f;
        currentBoostAcceleration = 0f;
        jumpStartHeight = 0f;

        // 清除空中控制状态
        airHorizontalVelocity = Vector3.zero;
        initialInheritedVelocity = Vector3.zero;

        // DOTween着陆回弹动画
        StartLandingBounceAnimation();

        // 检查是否有输入缓存，如果有则立即执行跳跃
        if (hasJumpBufferedInput && jumpBufferCounter > 0f)
        {
            JumpLogger.LogYellow($"检测到输入缓存，自动执行跳跃");
            hasJumpBufferedInput = false;
            jumpBufferCounter = 0f;

            // 延迟一帧执行跳跃，确保当前帧的着陆状态已完全处理
            StartCoroutine(DelayedJump());
        }
        
        // 重置土狼机制状态
        coyoteTimeCounter = 0f;
        wasOnGroundLastFrame = false;
    }

    /// <summary>
    /// 延迟执行跳跃（用于输入缓存）
    /// </summary>
    private System.Collections.IEnumerator DelayedJump()
    {
        yield return null; // 等待一帧

        if (CanJump())
        {
            StartJump();
        }
    }

    /// <summary>
    /// 空中水平控制系统 - 继承惯性+自然衰减+方向键微调
    /// </summary>
    private void UpdateAirControl()
    {
        if (configManager == null) return;

        // 1. 应用空气阻力（时间相关的指数衰减，帧率无关）
        // AirDragFactor应该改为每秒的衰减率，而不是每帧
        airHorizontalVelocity *= Mathf.Pow(configManager.AirDragFactor, Time.deltaTime * 5);
        //JumpLogger.LogWhite(airHorizontalVelocity.magnitude);

        // 2. 获取当前移动输入
        Vector3 currentMoveInput = movement.MoveInput;

        // 3. 如果有方向键输入，进行空中微调
        if (currentMoveInput.magnitude > 0.02f && characterMainControl.CanMove())
        {
            // 在惯性基础上叠加空中控制加速度
            float airAcceleration = movement.Running ? movement.runAcc : movement.walkAcc;
            airAcceleration *= configManager.AirControlFactor; // 使用配置的空中控制系数

            // 计算当前帧的空中控制加速度向量
            Vector3 airControlAcceleration = currentMoveInput * (airAcceleration * Time.deltaTime);

            // 在惯性基础上叠加加速度（而不是替换速度）
            Vector3 newHorizontalVelocity = airHorizontalVelocity + airControlAcceleration;

            // 限制最大速度（防止无限加速）
            float maxSpeed = movement.Running ? movement.runSpeed : movement.walkSpeed;
            if (newHorizontalVelocity.magnitude > maxSpeed)
            {
                newHorizontalVelocity = newHorizontalVelocity.normalized * maxSpeed;
            }

            airHorizontalVelocity = newHorizontalVelocity;

            //JumpLogger.LogWhite($"空中微调 - 加速度: ({airControlAcceleration.x:F2}, {airControlAcceleration.z:F2}), 新速度: ({airHorizontalVelocity.x:F2}, {airHorizontalVelocity.z:F2})");
        }

        // 4. 应用最终的水平速度到角色
        Vector3 finalVelocity = characterMovement.velocity;
        finalVelocity.x = airHorizontalVelocity.x;
        finalVelocity.z = airHorizontalVelocity.z;
        characterMovement.velocity = finalVelocity;
    }

    /// <summary>
    /// 播放跳跃音效
    /// </summary>
    private void PlayJumpSound()
    {
        var audioFile = ModBehaviour.GetRandomAudioFile();
        if (audioFile == null) return;
        if (jumpSoundInstance.HasValue && jumpSoundInstance.Value.isValid())
        {
            jumpSoundInstance.Value.stop(STOP_MODE.IMMEDIATE);
            jumpSoundInstance.Value.release();
        }
        jumpSoundInstance = Duckov.AudioManager.PostCustomSFX(audioFile, characterMainControl.gameObject);
    }

    /// <summary>
    /// 重置跳跃状态
    /// </summary>
    public void ResetJumpState()
    {
        isJumping = false;
        isJumpingHeld = false;
        jumpReleased = false; // 重置释放状态
        currentJumpPower = 0f;
        currentBoostAcceleration = 0f;
        jumpStartHeight = 0f;

        // 清除空中控制状态
        airHorizontalVelocity = Vector3.zero;
        initialInheritedVelocity = Vector3.zero;

        // 重置输入缓存状态
        hasJumpBufferedInput = false;
        jumpBufferCounter = 0f;

        // 重置土狼机制状态
        coyoteTimeCounter = 0f;
        wasOnGroundLastFrame = false;

        // 清除所有Scale动画
        jumpScaleTween?.Kill();

        // 清除跳跃音效引用
        jumpSoundInstance = null;
    }
    
    private void OnDestroy()
    {
        if (jumpSoundInstance.HasValue && jumpSoundInstance.Value.isValid())
        {
            jumpSoundInstance.Value.stop(STOP_MODE.IMMEDIATE);
            jumpSoundInstance.Value.release();
            jumpSoundInstance = null;
        }
    }
}