using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LiveKit;

[RequireComponent(typeof(RawImage))]
public class CameraStreamPlayer : MonoBehaviour
{
    private RawImage _display;
    private VideoStream _videoStream;
    private Coroutine _streamCoroutine;
    private RemoteTrackPublication _currentPub;

    void Awake()
    {
        _display = GetComponent<RawImage>();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    public void SubscribeTo(RemoteTrackPublication pub)
    {
        if (pub == null) return;

        if (_currentPub != null && _currentPub != pub)
        {
            _currentPub.SetSubscribed(false);
            StopStream();
        }

        _currentPub = pub;
        LiveKitManager.Instance.OnTrackSubscribed += OnTrackSubscribed;
        pub.SetSubscribed(true);
    }

    public void Unsubscribe()
    {
        if (LiveKitManager.Instance != null)
            LiveKitManager.Instance.OnTrackSubscribed -= OnTrackSubscribed;

        if (_currentPub != null)
        {
            _currentPub.SetSubscribed(false);
            _currentPub = null;
        }

        StopStream();
    }

    private void OnTrackSubscribed(IRemoteTrack track, RemoteTrackPublication pub, RemoteParticipant participant)
    {
        if (pub != _currentPub) return;
        if (track is not RemoteVideoTrack videoTrack) return;

        LiveKitManager.Instance.OnTrackSubscribed -= OnTrackSubscribed;

        StopStream();
        _videoStream = new VideoStream(videoTrack);
        _videoStream.TextureReceived += OnTextureReceived;
        _videoStream.Start();
        _streamCoroutine = StartCoroutine(_videoStream.Update());
    }

    private void OnTextureReceived(Texture tex)
    {
        _display.texture = tex;
    }

    private void StopStream()
    {
        if (_streamCoroutine != null)
        {
            StopCoroutine(_streamCoroutine);
            _streamCoroutine = null;
        }

        if (_videoStream != null)
        {
            _videoStream.TextureReceived -= OnTextureReceived;
            _videoStream.Stop();
            _videoStream.Dispose();
            _videoStream = null;
        }

        if (_display != null)
            _display.texture = null;
    }
}
