﻿using Assets.Scripts.Types;
using System;
using UnityEngine;
#nullable enable
public class HoldDrop : NoteLongDrop
{
    public bool isEX;
    public bool isBreak;

    public Sprite tapSpr;
    public Sprite holdOnSpr;
    public Sprite holdOffSpr;
    public Sprite eachSpr;
    public Sprite eachHoldOnSpr;
    public Sprite exSpr;
    public Sprite breakSpr;
    public Sprite breakHoldOnSpr;

    public Sprite eachLine;
    public Sprite breakLine;

    public Sprite holdEachEnd;
    public Sprite holdBreakEnd;

    public RuntimeAnimatorController HoldShine;
    public RuntimeAnimatorController BreakShine;

    public GameObject tapLine;

    public Color exEffectTap;
    public Color exEffectEach;
    public Color exEffectBreak;
    private Animator animator;

    public Material breakMaterial;

    private SpriteRenderer exSpriteRender;
    private bool holdAnimStart;
    private SpriteRenderer holdEndRender;
    private SpriteRenderer lineSpriteRender;

    private SpriteRenderer spriteRenderer;


    private void Start()
    {
        var notes = GameObject.Find("Notes").transform;
        objectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();
        noteManager = notes.GetComponent<NoteManager>();
        holdEffect = Instantiate(holdEffect, notes);
        holdEffect.SetActive(false);

        tapLine = Instantiate(tapLine, notes);
        tapLine.SetActive(false);
        lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();

        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();

        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        holdEndRender = transform.GetChild(1).GetComponent<SpriteRenderer>();

        spriteRenderer.sortingOrder += noteSortOrder;
        exSpriteRender.sortingOrder += noteSortOrder;
        holdEndRender.sortingOrder += noteSortOrder;

        spriteRenderer.sprite = tapSpr;
        exSpriteRender.sprite = exSpr;

        var anim = gameObject.AddComponent<Animator>();
        anim.enabled = false;
        animator = anim;

        if (isEX) exSpriteRender.color = exEffectTap;
        if (isEach)
        {
            spriteRenderer.sprite = eachSpr;
            lineSpriteRender.sprite = eachLine;
            holdEndRender.sprite = holdEachEnd;
            if (isEX) exSpriteRender.color = exEffectEach;
        }

        if (isBreak)
        {
            spriteRenderer.sprite = breakSpr;
            lineSpriteRender.sprite = breakLine;
            holdEndRender.sprite = holdBreakEnd;
            if (isEX) exSpriteRender.color = exEffectBreak;
            spriteRenderer.material = breakMaterial;
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
        holdEndRender.enabled = false;

        sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
        manager = GameObject.Find("Sensors")
                                .GetComponent<SensorManager>();
        inputManager = GameObject.Find("Input")
                                 .GetComponent<InputManager>();
        sensorPos = (SensorType)(startPosition - 1);
        inputManager.BindArea(Check, sensorPos);
    }
    private void FixedUpdate()
    {
        var timing = GetJudgeTiming();
        var remainingTime = GetRemainingTime();

        if (remainingTime == 0 && isJudged) // Hold完成后Destroy
        {
            Destroy(tapLine);
            Destroy(holdEffect);
            Destroy(gameObject);
        }
        else if(timing >= -0.01f)
        {
            // AutoPlay相关
            switch (InputManager.Mode)
            {
                case AutoPlayMode.Enable:
                    if(!isJudged)
                        objectCounter.NextNote(startPosition);
                    judgeResult = JudgeType.Perfect;
                    isJudged = true;
                    PlayHoldEffect();
                    return;
                case AutoPlayMode.DJAuto:
                    if (!isJudged)
                        manager.SetSensorOn(sensor.Type, guid);
                    break;
                case AutoPlayMode.Random:
                    if (!isJudged)
                    {
                        objectCounter.NextNote(startPosition);
                        judgeResult = (JudgeType)UnityEngine.Random.Range(1, 14);
                        isJudged = true;
                    }
                    PlayHoldEffect();
                    return;
                case AutoPlayMode.Disable:
                    manager.SetSensorOff(sensor.Type, guid);
                    break;
            }
        }

        if (isJudged) // 头部判定完成后开始累计按压时长
        {
            if (timing <= 0.1f) // 忽略头部6帧
                return;
            else if (remainingTime <= 0.2f) // 忽略尾部12帧
                return;
            else if (!timeProvider.isStart) // 忽略暂停
                return;
            var on = inputManager.CheckAreaStatus(sensorPos,SensorStatus.On);
            if (on)
                PlayHoldEffect();
            else
            {
                playerIdleTime += Time.fixedDeltaTime;
                StopHoldEffect();
            }
        }
        else if (timing > 0.15f && !isJudged) // 头部Miss
        {
            judgeDiff = 150;
            judgeResult = JudgeType.Miss;
            isJudged = true;
            objectCounter.NextNote(startPosition);
        }
    }
    void Check(object sender, InputEventArgs arg)
    {
        if (arg.Type != sensor.Type)
            return;
        else if (isJudged || !noteManager.CanJudge(gameObject, startPosition))
            return;
        else if (InputManager.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
            return;
        if (arg.IsClick)
        {
            if (!inputManager.IsIdle(arg))
                return;
            else
                inputManager.SetBusy(arg);
            Judge();
            if (isJudged)
            {
                inputManager.UnbindArea(Check, sensorPos);
                objectCounter.NextNote(startPosition);
            }
        }
    }
    void Judge()
    {

        const int JUDGE_GOOD_AREA = 150;
        const int JUDGE_GREAT_AREA = 100;
        const int JUDGE_PERFECT_AREA = 50;

        const float JUDGE_SEG_PERFECT1 = 16.66667f;
        const float JUDGE_SEG_PERFECT2 = 33.33334f;
        const float JUDGE_SEG_GREAT1 = 66.66667f;
        const float JUDGE_SEG_GREAT2 = 83.33334f;

        if (isJudged)
            return;

        var timing = timeProvider.AudioTime - time;
        var isFast = timing < 0;
        var diff = MathF.Abs(timing * 1000);
        JudgeType result;
        if (diff > JUDGE_GOOD_AREA && isFast)
            return;
        else if (diff < JUDGE_SEG_PERFECT1)
            result = JudgeType.Perfect;
        else if (diff < JUDGE_SEG_PERFECT2)
            result = JudgeType.LatePerfect1;
        else if (diff < JUDGE_PERFECT_AREA)
            result = JudgeType.LatePerfect2;
        else if (diff < JUDGE_SEG_GREAT1)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_SEG_GREAT2)
            result = JudgeType.LateGreat1;
        else if (diff < JUDGE_GREAT_AREA)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_GOOD_AREA)
            result = JudgeType.LateGood;
        else
            result = JudgeType.Miss;

        if (result != JudgeType.Miss && isFast)
            result = 14 - result;
        if (result != JudgeType.Miss && isEX)
            result = JudgeType.Perfect;
        if (isFast)
            judgeDiff = 0;
        else
            judgeDiff = diff;

        judgeResult = result;
        isJudged = true;
        PlayHoldEffect();
    }
    // Update is called once per frame
    private void Update()
    {
        var timing = GetJudgeTiming();
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        if (destScale < 0f)
        {
            destScale = 0f;
            return;
        }

        spriteRenderer.forceRenderingOff = false;
        if (isEX) exSpriteRender.forceRenderingOff = false;

        spriteRenderer.size = new Vector2(1.22f, 1.4f);

        var holdTime = timing - LastFor;
        var holdDistance = holdTime * speed + 4.8f;
        if (holdTime >= 0 || 
            holdTime >= 0 && LastFor <= 0.15f)
        {
            tapLine.transform.localScale = new Vector3(1f, 1f, 1f);
            transform.position = getPositionFromDistance(4.8f);
            return;
        }


        transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
        tapLine.transform.rotation = transform.rotation;
        holdEffect.transform.position = getPositionFromDistance(4.8f);

        if (isBreak &&
            !holdAnimStart && 
            !isJudged)
        {
            var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
        }


        if (destScale > 0.3f) tapLine.SetActive(true);

        if (distance < 1.225f)
        {
            transform.localScale = new Vector3(destScale, destScale);
            spriteRenderer.size = new Vector2(1.22f, 1.42f);
            distance = 1.225f;
            var pos = getPositionFromDistance(distance);
            transform.position = pos;            
        }
        else
        {
            if (holdDistance < 1.225f && distance >= 4.8f) // 头到达 尾未出现
            {
                holdDistance = 1.225f;
                distance = 4.8f;
            }
            else if (holdDistance < 1.225f && distance < 4.8f) // 头未到达 尾未出现
            {
                holdDistance = 1.225f;
            }
            else if (holdDistance >= 1.225f && distance >= 4.8f) // 头到达 尾出现
            {
                distance = 4.8f;

                holdEndRender.enabled = true;
            }
            else if (holdDistance >= 1.225f && distance < 4.8f) // 头未到达 尾出现
            {
                holdEndRender.enabled = true;
            }

            var dis = (distance - holdDistance) / 2 + holdDistance;
            transform.position = getPositionFromDistance(dis); //0.325
            var size = distance - holdDistance + 1.4f;
            spriteRenderer.size = new Vector2(1.22f, size);
            holdEndRender.transform.localPosition = new Vector3(0f, 0.6825f - size / 2);
            transform.localScale = new Vector3(1f, 1f);
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        lineScale = lineScale >= 1f ? 1f : lineScale;
        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
        exSpriteRender.size = spriteRenderer.size;
    }
    private void OnDestroy()
    {
        if (HttpHandler.IsReloding)
            return;
        var realityHT = LastFor - 0.3f - (judgeDiff / 1000f);
        var percent = MathF.Min(1, (realityHT - playerIdleTime) / realityHT);
        JudgeType result = judgeResult;
        if(realityHT > 0)
        {
            if (percent >= 1f)
            {
                if(judgeResult == JudgeType.Miss)
                    result = JudgeType.LateGood;
                else if (MathF.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else
                    result = judgeResult;
            }
            else if (percent >= 0.67f)
            {
                if (judgeResult == JudgeType.Miss)
                    result = JudgeType.LateGood;
                else if (MathF.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else if (judgeResult == JudgeType.Perfect)
                    result = (int)judgeResult < 7 ? JudgeType.LatePerfect1 : JudgeType.FastPerfect1;
            }
            else if (percent >= 0.33f)
            {
                if (MathF.Abs((int)judgeResult - 7) >= 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
                else
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
            }
            else if (percent >= 0.05f)
                result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            else if (percent >= 0)
            {
                if (judgeResult == JudgeType.Miss)
                    result = JudgeType.Miss;
                else
                    result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            }
        }

        switch (InputManager.Mode)
        {
            case AutoPlayMode.Enable:
                result = JudgeType.Perfect;
                break;
            case AutoPlayMode.Random:
                result = (JudgeType)UnityEngine.Random.Range(1, 14);
                break;
            case AutoPlayMode.DJAuto:
            case AutoPlayMode.Disable:
                break;
        }
        var effectManager = GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>();
        effectManager.PlayEffect(startPosition, isBreak, result);
        effectManager.PlayFastLate(startPosition, result);
        print($"Hold: {MathF.Round(percent * 100,2)}%\nTotal Len : {MathF.Round(realityHT * 1000,2)}ms");

        objectCounter.ReportResult(this, result, isBreak);
        if (!isJudged)
            objectCounter.NextNote(startPosition);

        manager.SetSensorOff(sensor.Type, guid);
        inputManager.UnbindArea(Check, sensorPos);
    }
    protected override void PlayHoldEffect()
    {
        base.PlayHoldEffect();
        GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>().ResetEffect(startPosition);
        if (LastFor <= 0.3)
            return;
        else if (!holdAnimStart && GetJudgeTiming() >= 0.1f)//忽略开头6帧与结尾12帧
        {
            holdAnimStart = true;
            animator.runtimeAnimatorController = HoldShine;
            animator.enabled = true;
            var sprRenderer = GetComponent<SpriteRenderer>();
            if (isBreak)
                sprRenderer.sprite = breakHoldOnSpr;
            else if (isEach)
                sprRenderer.sprite = eachHoldOnSpr;
            else
                sprRenderer.sprite = holdOnSpr;
            if (judgeResult == JudgeType.Miss)
        }
    }
    protected override void StopHoldEffect()
    {
        base.StopHoldEffect();
        holdAnimStart = false;
        animator.runtimeAnimatorController = HoldShine;
        animator.enabled = false;
        var sprRenderer = GetComponent<SpriteRenderer>();
        sprRenderer.sprite = holdOffSpr;
    }

}