using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiveKit;
using LiveKit.Proto;

public class LiveKitManager : MonoBehaviour
{
    public static LiveKitManager Instance { get; private set; }

    [Header("Connection")]
    [SerializeField] private string serverUrl = "ws://localhost:7880";
    [SerializeField] private string token = "";
    [SerializeField] private bool autoConnect = true;

    public Room Room { get; private set; }
    public bool IsConnected => Room?.IsConnected ?? false;

    // Keyed by participant identity
    private readonly Dictionary<string, RemoteTrackPublication> _videoTracks = new();
    public IReadOnlyDictionary<string, RemoteTrackPublication> VideoTracks => _videoTracks;

    public event Action<Room> OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnVideoTrackAvailable;
    public event Action<string> OnVideoTrackRemoved;
    public event Action<IRemoteTrack, RemoteTrackPublication, RemoteParticipant> OnTrackSubscribed;
    public event Action<IRemoteTrack, RemoteTrackPublication, RemoteParticipant> OnTrackUnsubscribed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (autoConnect)
            StartCoroutine(Connect());
    }

    void OnDestroy()
    {
        Room?.Disconnect();
    }

    public IEnumerator Connect()
    {
        Room = new Room();
        Room.TrackPublished += HandleTrackPublished;
        Room.TrackUnpublished += HandleTrackUnpublished;
        Room.TrackSubscribed += HandleTrackSubscribed;
        Room.TrackUnsubscribed += HandleTrackUnsubscribed;
        Room.Disconnected += HandleDisconnected;

        var options = new LiveKit.RoomOptions { AutoSubscribe = false };
        var connect = Room.Connect(serverUrl, token, options);
        yield return connect;

        if (connect.IsError)
        {
            Debug.LogError("[LiveKit] Failed to connect to server");
            yield break;
        }

        Debug.Log($"[LiveKit] Connected to room: {Room.Name}");

        foreach (var kv in Room.RemoteParticipants)
            RegisterParticipantTracks(kv.Value);

        OnConnected?.Invoke(Room);
    }

    private void RegisterParticipantTracks(RemoteParticipant participant)
    {
        foreach (var kv in participant.Tracks)
        {
            if (kv.Value.Kind == TrackKind.KindVideo)
                AddVideoTrack(participant.Identity, kv.Value);
        }
    }

    private void HandleTrackPublished(RemoteTrackPublication pub, RemoteParticipant participant)
    {
        if (pub.Kind == TrackKind.KindVideo)
            AddVideoTrack(participant.Identity, pub);
    }

    private void HandleTrackUnpublished(RemoteTrackPublication pub, RemoteParticipant participant)
    {
        if (_videoTracks.Remove(participant.Identity))
            OnVideoTrackRemoved?.Invoke(participant.Identity);
    }

    private void HandleTrackSubscribed(IRemoteTrack track, RemoteTrackPublication pub, RemoteParticipant participant)
    {
        OnTrackSubscribed?.Invoke(track, pub, participant);
    }

    private void HandleTrackUnsubscribed(IRemoteTrack track, RemoteTrackPublication pub, RemoteParticipant participant)
    {
        OnTrackUnsubscribed?.Invoke(track, pub, participant);
    }

    private void HandleDisconnected(Room room)
    {
        Debug.Log("[LiveKit] Disconnected");
        _videoTracks.Clear();
        OnDisconnected?.Invoke();
    }

    private void AddVideoTrack(string identity, RemoteTrackPublication pub)
    {
        _videoTracks[identity] = pub;
        OnVideoTrackAvailable?.Invoke(identity);
    }
}
