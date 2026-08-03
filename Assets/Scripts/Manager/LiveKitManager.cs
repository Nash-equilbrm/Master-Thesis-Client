using System;
using System.Collections;
using System.Collections.Generic;
using LiveKit;
using LiveKit.Proto;
using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public class LiveKitManager : Singleton<LiveKitManager>
    {
        [Header("Connection")]
        [SerializeField] private string _serverUrl = "ws://localhost:7880";
        [SerializeField] private string _token = "";
        [SerializeField] private bool _autoConnect = false;

        public Room Room { get; private set; }
        public bool IsConnected => Room?.IsConnected ?? false;
        public bool IsConnecting { get; private set; }

        public string ServerUrl { get => _serverUrl; set => _serverUrl = value; }
        public string Token { get => _token; set => _token = value; }

        private readonly Dictionary<string, RemoteTrackPublication> _videoTracks = new();
        public IReadOnlyDictionary<string, RemoteTrackPublication> VideoTracks => _videoTracks;

        public event Action<Room> OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnConnectionError;
        public event Action<string> OnVideoTrackAvailable;
        public event Action<string> OnVideoTrackRemoved;
        public event Action<IRemoteTrack, RemoteTrackPublication, RemoteParticipant> OnTrackSubscribed;
        public event Action<IRemoteTrack, RemoteTrackPublication, RemoteParticipant> OnTrackUnsubscribed;

        protected override void Awake()
        {
            base.Awake();
        }

        void Start()
        {
            if (_autoConnect)
                StartCoroutine(Connect());
        }

        public void ConnectWith(string url, string accessToken)
        {
            _serverUrl = url;
            _token = accessToken;
            if (IsConnected || IsConnecting) return;
            StartCoroutine(Connect());
        }

        void OnDestroy()
        {
            Room?.Disconnect();
        }

        public IEnumerator Connect()
        {
            if (IsConnected || IsConnecting) yield break;
            IsConnecting = true;

            Room = new Room();
            Room.TrackPublished += HandleTrackPublished;
            Room.TrackUnpublished += HandleTrackUnpublished;
            Room.TrackSubscribed += HandleTrackSubscribed;
            Room.TrackUnsubscribed += HandleTrackUnsubscribed;
            Room.Disconnected += HandleDisconnected;

            var options = new LiveKit.RoomOptions { AutoSubscribe = false };
            var connect = Room.Connect(_serverUrl, _token, options);
            yield return connect;

            if (connect.IsError)
            {
                IsConnecting = false;
                Debug.LogError("[LiveKitManager] Failed to connect to server");
                OnConnectionError?.Invoke("Unable to connect. Check the server URL and token.");
                yield break;
            }

            IsConnecting = false;
            Debug.Log($"[LiveKitManager] Connected to room: {Room.Name}");

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
            Debug.Log("[LiveKitManager] Disconnected");
            _videoTracks.Clear();
            OnDisconnected?.Invoke();
        }

        private void AddVideoTrack(string identity, RemoteTrackPublication pub)
        {
            _videoTracks[identity] = pub;
            OnVideoTrackAvailable?.Invoke(identity);
        }
    }
}
