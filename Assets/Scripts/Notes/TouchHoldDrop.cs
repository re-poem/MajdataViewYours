﻿using Assets.Scripts;
using Assets.Scripts.Types;
using System;
using UnityEngine;
#nullable enable
public class TouchHoldDrop : TouchHoldBase
{
    public Sprite touchHoldBoard;
    public Sprite touchHoldBoard_Miss;
    public SpriteRenderer boarder;
    public Sprite[] TouchHoldSprite = new Sprite[5];
    public Sprite TouchPointSprite;
    public Sprite TouchPointEachSprite;

    public GameObject[] fans;

    public SpriteMask mask;
    private readonly SpriteRenderer[] fansSprite = new SpriteRenderer[6];
    private float displayDuration;

    private GameObject firework;
    private Animator fireworkEffect;
    private float moveDuration;

    private float wholeDuration;

    // Start is called before the first frame update
    private void Start()
    {
        wholeDuration = 3.209385682f * Mathf.Pow(speed, -0.9549621752f);
        moveDuration = 0.8f * wholeDuration;
        displayDuration = 0.2f * wholeDuration;

        objectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();
        var notes = GameObject.Find("Notes").transform;
        noteManager = notes.GetComponent<NoteManager>();
        holdEffect = Instantiate(holdEffect, notes);
        holdEffect.SetActive(false);

        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();

        firework = GameObject.Find("FireworkEffect");
        fireworkEffect = firework.GetComponent<Animator>();

        for (var i = 0; i < 6; i++)
        {
            fansSprite[i] = fans[i].GetComponent<SpriteRenderer>();
            fansSprite[i].sortingOrder += noteSortOrder;
        }

        for (var i = 0; i < 4; i++) fansSprite[i].sprite = TouchHoldSprite[i];
        fansSprite[5].sprite = TouchHoldSprite[4]; // TouchHold Border
        if (isEach)
        {
            fansSprite[4].sprite = TouchPointEachSprite;
        }
        else
        {
            fansSprite[4].sprite = TouchPointSprite;
        }

        transform.position = GetAreaPos(startPosition, areaPosition);


        SetfanColor(new Color(1f, 1f, 1f, 0f));

        mask.backSortingOrder = fansSprite[5].sortingOrder - 1;
        mask.frontSortingOrder = fansSprite[5].sortingOrder;
        mask.enabled = false;

        sensor = GameObject.Find("Sensors")
                                   .transform.GetChild((int)GetSensor())
                                   .GetComponent<Sensor>();
        manager = GameObject.Find("Sensors")
                                .GetComponent<SensorManager>();
        inputManager = GameObject.Find("Input")
                                 .GetComponent<InputManager>();
        var customSkin = GameObject.Find("Outline").GetComponent<CustomSkin>();
        judgeText = customSkin.JudgeText;
        inputManager.BindSensor(Check, GetSensor());
    }
    void Check(object sender, InputEventArgs arg)
    {
        if (isJudged || !noteManager.CanJudge(gameObject, sensor.Type))
            return;
        else if (InputManager.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
            return;
        else if (arg.IsClick)
        {
            if (!inputManager.IsIdle(arg))
                return;
            else
                inputManager.SetBusy(arg);
            Judge();
            if (isJudged)
            {
                inputManager.UnbindSensor(Check, GetSensor());
                objectCounter.NextTouch(GetSensor());
            }
        }
    }
    void Judge()
    {

        const float JUDGE_GOOD_AREA = 316.667f;
        const int JUDGE_GREAT_AREA = 250;
        const int JUDGE_PERFECT_AREA = 200;

        const float JUDGE_SEG_PERFECT = 150f;

        if (isJudged)
            return;

        var timing = timeProvider.AudioTime - time;
        var isFast = timing < 0;
        var diff = MathF.Abs(timing * 1000);
        JudgeType result;
        if (diff > JUDGE_SEG_PERFECT && isFast)
            return;
        else if (diff < JUDGE_SEG_PERFECT)
            result = JudgeType.Perfect;
        else if (diff < JUDGE_PERFECT_AREA)
            result = JudgeType.LatePerfect2;
        else if (diff < JUDGE_GREAT_AREA)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_GOOD_AREA)
            result = JudgeType.LateGood;
        else
            result = JudgeType.Miss;
        if (isFast)
            judgeDiff = 0;
        else
            judgeDiff = diff;

        judgeResult = result;
        isJudged = true;
        PlayHoldEffect();
    }
    private void FixedUpdate()
    {
        var remainingTime = GetRemainingTime();
        var timing = GetJudgeTiming();
        var holdTime = timing - LastFor;

        if (remainingTime == 0 && isJudged)
        {
            Destroy(holdEffect);
            Destroy(gameObject);
        }
        else if (timing >= -0.01f)
        {
            // AutoPlay相关
            switch (InputManager.Mode)
            {
                case AutoPlayMode.Enable:
                    if (!isJudged)
                        objectCounter.NextTouch(GetSensor());
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
                        objectCounter.NextTouch(GetSensor());
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

        if (isJudged)
        {
            if (timing <= 0.25f) // 忽略头部15帧
                return;
            else if (remainingTime <= 0.2f) // 忽略尾部12帧
                return;
            else if (!timeProvider.isStart) // 忽略暂停
                return;

            var on = inputManager.CheckSensorStatus(GetSensor(), SensorStatus.On);
            if (on)
                PlayHoldEffect();
            else
            {
                playerIdleTime += Time.fixedDeltaTime;
                StopHoldEffect();
            }
        }
        else if (timing > 0.316667f)
        {
            judgeDiff = 316.667f;
            judgeResult = JudgeType.Miss;
            inputManager.UnbindSensor(Check, GetSensor());
            isJudged = true;
            objectCounter.NextTouch(GetSensor());
        }
    }
    // Update is called once per frame
    private void Update()
    {
        var timing = GetJudgeTiming();
        var pow = -Mathf.Exp(8 * (timing * 0.4f / moveDuration) - 0.85f) + 0.42f;
        var distance = Mathf.Clamp(pow, 0f, 0.4f);

        if (-timing <= wholeDuration && -timing > moveDuration)
        {
            SetfanColor(new Color(1f, 1f, 1f, Mathf.Clamp((wholeDuration + timing) / displayDuration, 0f, 1f)));
            fans[5].SetActive(false);
            mask.enabled = false;
        }
        else if (-timing < moveDuration)
        {
            fans[5].SetActive(true);
            mask.enabled = true;
            SetfanColor(Color.white);
            mask.alphaCutoff = Mathf.Clamp(0.91f * (1 - (LastFor - timing) / LastFor), 0f, 1f);
        }

        if (float.IsNaN(distance)) distance = 0f;
        if (distance == 0f)
        {
            //holdEffect.SetActive(true);
            holdEffect.transform.position = transform.position;
        }
        for (var i = 0; i < 4; i++)
        {
            var pos = (0.226f + distance) * GetAngle(i);
            fans[i].transform.localPosition = pos;
        }
    }

    Vector3 GetAreaPos(int index, char area)
    {
        /// <summary>
        /// AreaDistance: 
        /// C:   0
        /// E:   3.1
        /// B:   2.21
        /// A,D: 4.8
        /// </summary>
        if (area == 'C') return Vector3.zero;
        if (area == 'B')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 5) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.3f;
        }
        if (area == 'A')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 5) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.1f;
        }
        if (area == 'E')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 6) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.0f;
        }
        if (area == 'D')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 6) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.1f;
        }
        return Vector3.zero;
    }
    private void OnDestroy()
    {
        if (HttpHandler.IsReloding)
            return;
        var realityHT = LastFor - 0.45f - (judgeDiff / 1000f);
        var percent = MathF.Min(1, (realityHT - playerIdleTime) / realityHT);
        JudgeType result = judgeResult;
        if (realityHT > 0)
        {
            if (percent >= 1f)
            {
                if (judgeResult == JudgeType.Miss)
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

        print($"TouchHold: {MathF.Round(percent * 100, 2)}%\nTotal Len : {MathF.Round(realityHT * 1000, 2)}ms");
        objectCounter.ReportResult(this, result);
        if (!isJudged)
            objectCounter.NextTouch(GetSensor());
        if (isFirework && result != JudgeType.Miss)
        {
            fireworkEffect.SetTrigger("Fire");
            firework.transform.position = transform.position;
        }
        inputManager.UnbindSensor(Check, GetSensor());
        manager.SetSensorOff(sensor.Type, guid);
        PlayJudgeEffect(result);
    }

    protected override void PlayHoldEffect()
    {
        base.PlayHoldEffect();
        boarder.sprite = touchHoldBoard;
    }
    void PlayJudgeEffect(JudgeType judgeResult)
    {
        var obj = Instantiate(judgeEffect, Vector3.zero, transform.rotation);
        var _obj = Instantiate(judgeEffect, Vector3.zero, transform.rotation);
        var judgeObj = obj.transform.GetChild(0);
        var flObj = _obj.transform.GetChild(0);

        if (sensor.Group != SensorGroup.C)
        {
            judgeObj.transform.position = GetPosition(-0.46f);
            flObj.transform.position = GetPosition(-0.92f);
        }
        else
        {
            judgeObj.transform.position = new Vector3(0, -0.6f, 0);
            flObj.transform.position = new Vector3(0, -1.08f, 0);
        }

        flObj.GetChild(0).transform.rotation = GetRoation();
        judgeObj.GetChild(0).transform.rotation = GetRoation();
        var anim = obj.GetComponent<Animator>();

        var effects = GameObject.Find("NoteEffects");
        var flAnim = _obj.GetComponent<Animator>();
        GameObject effect;
        switch (judgeResult)
        {
            case JudgeType.LateGood:
            case JudgeType.FastGood:
                judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = judgeText[1];
                effect = Instantiate(effects.transform.GetChild(3).GetChild(0), transform.position, transform.rotation).gameObject;
                effect.SetActive(true);
                break;
            case JudgeType.LateGreat:
            case JudgeType.LateGreat1:
            case JudgeType.LateGreat2:
            case JudgeType.FastGreat2:
            case JudgeType.FastGreat1:
            case JudgeType.FastGreat:
                judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = judgeText[2];
                //transform.Rotate(0, 0f, 30f);
                effect = Instantiate(effects.transform.GetChild(2).GetChild(0), transform.position, transform.rotation).gameObject;
                effect.SetActive(true);
                effect.gameObject.GetComponent<Animator>().SetTrigger("great");
                break;
            case JudgeType.LatePerfect2:
            case JudgeType.FastPerfect2:
            case JudgeType.LatePerfect1:
            case JudgeType.FastPerfect1:
                judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = judgeText[3];
                transform.Rotate(0, 180f, 90f);
                Instantiate(tapEffect, transform.position, transform.rotation);
                break;
            case JudgeType.Perfect:
                judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = judgeText[4];
                transform.Rotate(0, 180f, 90f);
                Instantiate(tapEffect, transform.position, transform.rotation);
                break;
            case JudgeType.Miss:
                judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = judgeText[0];
                break;
            default:
                break;
        }
        //judgeEffect.transform.position = new Vector3(0, -0.6f, 0);
        GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>().PlayFastLate(_obj, flAnim, judgeResult);
        anim.SetTrigger("touch");
    }
    protected override void StopHoldEffect()
    {
        base.StopHoldEffect();
        boarder.sprite = touchHoldBoard_Miss;
    }
    /// <summary>
    /// 获取当前坐标指定距离的坐标
    /// <para>方向：原点</para>
    /// </summary>
    /// <param name="magnitude"></param>
    /// <param name="distance"></param>
    /// <returns></returns>
    Vector3 GetPosition(float distance)
    {
        var d = transform.position.magnitude;
        var ratio = MathF.Max(0, d + distance) / d;
        return transform.position * ratio;
    }
    private Vector3 GetAngle(int index)
    {
        var angle = Mathf.PI / 4 + index * (Mathf.PI / 2);
        return new Vector3(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    private void SetfanColor(Color color)
    {
        foreach (var fan in fansSprite) fan.color = color;
    }
}