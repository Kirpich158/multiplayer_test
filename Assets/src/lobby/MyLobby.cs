using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.SceneManagement;

public class MyLobby : MonoBehaviour {
    [SerializeField] private string _lobbyName;
    [SerializeField] private int _maxPlayers;
    //[SerializeField] private TextMeshProUGUI _playerCount;

    private Lobby _hostLobby;
    private Lobby _joinedLobby;
    private string _playerName;

    private float _lobbyJPollTimer;
    private float _lobbyHPollTimer;

    private async void Start() {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () => {
            Debug.Log("signed in: " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        _playerName = "Kirpich" + Random.Range(0, 10).ToString();
        Debug.Log("Your name: " + _playerName);

        Application.runInBackground = true;
    }

    private void Update() {
        LobbyPoll();
        //if (_joinedLobby == null) {
        //    _playerCount.text = "0/2";
        //} else {
        //    _playerCount.text = _joinedLobby.Players.Count.ToString() + "/" + _joinedLobby.MaxPlayers.ToString();
        //}
    }

    public async void CreateLobby() {
        try {
            CreateLobbyOptions createLobbyOpts = new CreateLobbyOptions {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject> {
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, "Sandbox") },
                    { "Map", new DataObject(DataObject.VisibilityOptions.Public, "House") }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(_lobbyName, _maxPlayers, createLobbyOpts);
            _hostLobby = lobby;
            _joinedLobby = _hostLobby;

            Debug.Log("lobby created for " + lobby.MaxPlayers + " players with code \"" + lobby.LobbyCode + "\" to join");
            PrintPlayers(lobby);
            // starting a heartbeat for lobby so it won't get inactive on the lobby list
            StartCoroutine(LobbyHeartbeat(15f));

        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on lobby creation: " + exception);
        }
    }

    // recursive coroutine for heartbeating lobby
    private IEnumerator LobbyHeartbeat(float time) {
        if (_hostLobby != null) {
            yield return new WaitForSeconds(time);
            LobbyService.Instance.SendHeartbeatPingAsync(_hostLobby.Id);
            StartCoroutine(LobbyHeartbeat(time));
        }

        yield return null;
    }

    // timer for polling lobby updates
    private async void LobbyPoll() {
        if (_joinedLobby != null) {
            _lobbyJPollTimer -= Time.deltaTime;
            if (_lobbyJPollTimer < 0f) {
                float lobbyPollRate = 1.1f;
                _lobbyJPollTimer = lobbyPollRate;

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
                _joinedLobby = lobby;
            }
        }
    }

    public async void ShowLobbies() {
        try {
            QueryResponse response = await Lobbies.Instance.QueryLobbiesAsync();

            Debug.Log("found " + response.Results.Count + " avaliable lobbies");
            foreach (Lobby lobby in response.Results) {
                Debug.Log("in \"" + lobby.LobbyCode + "\" lobby (game mode: " + lobby.Data["GameMode"].Value + "; map: " + lobby.Data["Map"].Value + ") avaliable " + lobby.MaxPlayers + " players, join with code");
            }
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on lobby search: " + exception);
        }
    }

    public async void JoinLobbyByCode(TMP_InputField inputField) {
        try {
            JoinLobbyByCodeOptions joinLobbyCodeOpts = new JoinLobbyByCodeOptions { Player = GetPlayer() };

            Lobby joinedLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(inputField.text, joinLobbyCodeOpts);
            _joinedLobby = joinedLobby;

            Debug.Log("joined lobby with code");
            PrintPlayers(joinedLobby);
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on CODE join: " + exception);
        }
    }

    public async void QuickMM() {
        try {
            QuickJoinLobbyOptions quickLobbyOpts = new QuickJoinLobbyOptions { Player = GetPlayer() };

            Lobby joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync(quickLobbyOpts);
            _joinedLobby = joinedLobby;

            Debug.Log("joined lobby QUICK");
            PrintPlayers(joinedLobby);
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on QUICK join: " + exception);
        }
    }


    public void PrintPlayersInJoinedLobby() {
        PrintPlayers(_joinedLobby);
    }

    private void PrintPlayers(Lobby lobby) {
        Debug.Log("Players in the " + "\"" + lobby.Data["GameMode"].Value + "\" lobby with " + "\"" + lobby.LobbyCode + "\" code, on map \"" + lobby.Data["Map"].Value + "\": ");

        foreach (Player player in lobby.Players) {
            Debug.Log("Nickname: " + player.Data["PlayerName"].Value + "; ID: " + player.Id);
        }
    }

    private Player GetPlayer() {
        return new Player {
            Data = new Dictionary<string, PlayerDataObject> {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName) }
            }
        };
    }

    public async void UpdateLobbyGameMode(string newGameMode) {
        try {
            _hostLobby = await Lobbies.Instance.UpdateLobbyAsync(_hostLobby.Id, new UpdateLobbyOptions {
                Data = new Dictionary<string, DataObject> {
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, newGameMode)}
                }
            });
            _joinedLobby = _hostLobby;

            Debug.Log("changed lobby mode to " + _hostLobby.Data["GameMode"].Value);
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on lobby GameMode update: " + exception);
        }
    }

    public async void UpdatePlayerName(TMP_InputField inputField) {
        try {
            await Lobbies.Instance.UpdatePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions {
                Data = new Dictionary<string, PlayerDataObject> {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, inputField.text)}
                }
            });
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on player nickname change: " + exception);
        }
    }

    public async void LeaveLobby() {
        try {
            await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            _joinedLobby = _hostLobby = null;
            Debug.Log(AuthenticationService.Instance.PlayerName + " left the lobby");
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on player leaving: " + exception);
        }
    }
    
    // kicking another joined player (not host)
    public async void KickPlayer() {
        try {
            await LobbyService.Instance.RemovePlayerAsync(_hostLobby.Id, _hostLobby.Players[1].Id);
            Debug.Log("host kicked " + _hostLobby.Players[1].Data["PlayerName"].Value);
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on player kick: " + exception);
        }
    }

    public async void MigrateHost() {
        try {
            await Lobbies.Instance.UpdateLobbyAsync(_hostLobby.Id, new UpdateLobbyOptions {
                HostId = _hostLobby.Players[1].Id
            });
            _joinedLobby = _hostLobby;

            Debug.Log("host role migrated to " + _hostLobby.Players[1].Data["PlayerName"].Value);
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on migrating lobby's host: " + exception);
        }
    }

    public async void DeleteLobby() {
        try {
            await Lobbies.Instance.DeleteLobbyAsync(_hostLobby.Id);

            Debug.Log("host deleted lobby \"" + _hostLobby.LobbyCode + "\"");
        } catch (LobbyServiceException exception) {
            Debug.Log("exception caught on deleting lobby: " + exception);
        }
    }

    public async void StartTheGame() {
        if (_joinedLobby.Players.Count == 2) {
            await SceneManager.LoadSceneAsync(1);
        }
    }
}
