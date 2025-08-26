using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class CSLVPManager : MonoBehaviour
{
    public float playSpeed;

    private AudioTimeProvider provider;

    private float smoothRDelta;

    public SpriteRenderer spriteRender;

    public VideoPlayer videoPlayer;

    private float originalScaleX;
    // Start is called before the first frame update
    private void Start()
    {
        originalScaleX = gameObject.transform.localScale.x;
        //spriteRender = GetComponent<SpriteRenderer>();
        //videoPlayer = GetComponent<VideoPlayer>();
        provider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
    }

    private void Update()
    {
        //videoPlayer.externalReferenceTime = provider.AudioTime;
        var delta = (float)videoPlayer.clockTime - provider.AudioTime;
        smoothRDelta += (Time.unscaledDeltaTime - smoothRDelta) * 0.01f;
        if (provider.AudioTime < 0) return;
        var realSpeed = Time.deltaTime / smoothRDelta;

        if (Time.captureFramerate != 0)
        {
            //print("speed="+realSpeed+" delta="+delta);
            videoPlayer.playbackSpeed = realSpeed - delta;
            return;
        }

        if (delta < -0.01f)
            videoPlayer.playbackSpeed = playSpeed + 0.2f;
        else if (delta > 0.01f)
            videoPlayer.playbackSpeed = playSpeed - 0.2f;
        else
            videoPlayer.playbackSpeed = playSpeed;
    }

    public void PlayVideo()
    {
        videoPlayer.Play();
    }

    public void PauseVideo()
    {
        videoPlayer.Pause();
    }

    public void ContinueVideo(float speed)
    {
        videoPlayer.playbackSpeed = speed;
        playSpeed = speed;
        videoPlayer.Play();
    }


    public void LoadFromPath(string path)
    {
        loadVideo(path, playSpeed);
    }

    private void loadVideo(string path, float speed)
    {
        videoPlayer.url = "file://" + path;
        //videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.playbackSpeed = speed;
        playSpeed = speed;
        StartCoroutine(waitFumenStart());
    }

    private IEnumerator waitFumenStart()
    {
        while (provider == null) 
            provider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        videoPlayer.Prepare();
        //videoPlayer.timeReference = VideoTimeReference.ExternalTime;
        while (provider.AudioTime <= 0) yield return new WaitForEndOfFrame();
        while (!videoPlayer.isPrepared) yield return new WaitForEndOfFrame();
        videoPlayer.Play();
        videoPlayer.time = provider.AudioTime;

        var scale = videoPlayer.height / (float)videoPlayer.width;
        spriteRender.sprite =
            Sprite.Create(new Texture2D(1080, 1080), new Rect(0, 0, 1080, 1080), new Vector2(0.5f, 0.5f));

        gameObject.transform.localScale = new Vector3(originalScaleX, originalScaleX * scale);
    }
}