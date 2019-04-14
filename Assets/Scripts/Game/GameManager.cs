﻿using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;

namespace Com.Capra314Cabra.Project_2048Ex
{
    public class GameManager : MonoBehaviour
    {
        #region MonoBehaviour Instance (Edit on Inspector)

        [SerializeField]
        private Camera mainCamera;

        [SerializeField]
        private GUIBoardManager masterBoardGraphicManager;
        [SerializeField]
        private GUIBoardManager clientBoardGraphicManager;

        [SerializeField]
        private GameGUIManager gameGUIManager;

        #region For Count Down

        [SerializeField]
        private GameObject countDownGUI;
        [SerializeField]
        private Text countDownText;

        #endregion

        #endregion

        #region Game Logic Class Instance

        private BlockBoard masterBoard = new BlockBoard();
        private BlockBoard clientBoard = new BlockBoard();

        #endregion

        PhotonManager photonManager;
        IGameSyncer gameSyncer;

        // Start is called before the first frame update
        void Start()
        {
            gameGUIManager.PlayerAmount = 0;
            gameGUIManager.EnemyAmount = 0;

            if (GameStartArgment.OnlineGame)
            {
                Debug.Log("Unload Matching Scene...");
                StartCoroutine(UnloadMatchingScene());

                photonManager = GameObject.Find("PhotonManager").GetComponent<PhotonManager>();
                gameSyncer = photonManager;

                //
                // Debug code
                //
#if UNITY_EDITOR

                var master = PhotonNetwork.MasterClient;
                Debug.Log($"MasterNickName:{master.NickName}");
                var you = PhotonNetwork.NickName;
                Debug.Log($"YourNickName:{you}");
                Debug.Log($"You are \"{(PhotonNetwork.IsMasterClient ? "Master" : "Client")}\"");

#endif
            }
            else
            {
                gameSyncer = new GameSyncerOffline();
            }

            if(gameSyncer.PlayerStatus.IsClient())
            {
                var tmp = masterBoardGraphicManager;
                masterBoardGraphicManager = clientBoardGraphicManager;
                clientBoardGraphicManager = tmp;

                gameGUIManager.Swap();
            }
            masterBoardGraphicManager.ChangeGraphicAll(masterBoard);
            clientBoardGraphicManager.ChangeGraphicAll(clientBoard);

            gameSyncer.State = GameState.GAME_START;

            gameSyncer.OnAllPlayerReady += () =>
            {
                StartCoroutine(CountDown());
            };
            gameSyncer.OnGameStateChanged += OnGameStateChangedCallback;

            // The player is ready
            gameSyncer.Ready();
        }

        // Update is called once per frame
        void Update()
        {
            switch (gameSyncer.State)
            {
                case GameState.GAME_NOW:
                    {
                        if (!gameSyncer.PlayerStatus.IsWatcher())
                        {
                            if (Input.GetKeyDown(KeyCode.W)) InvokeBlockMoved(MoveDirection.UP);
                            else if (Input.GetKeyDown(KeyCode.S)) InvokeBlockMoved(MoveDirection.DOWN);
                            else if (Input.GetKeyDown(KeyCode.A)) InvokeBlockMoved(MoveDirection.LEFT);
                            else if (Input.GetKeyDown(KeyCode.D)) InvokeBlockMoved(MoveDirection.RIGHT);
                        }
                    }
                    break;
            }
            while(gameSyncer.DoneActions.Any())
            {
                var action = gameSyncer.DoneActions.Dequeue();
                DoAction(action);
            }
        }

        void DoAction(GameAction action)
        {
            switch(action.ActionType)
            {
                case ActionType.BLOCK_MOVED:
                    {
                        if (action.IsMaster)
                        {
                            masterBoard.Move((MoveDirection)action.Parameter, out _);
                            masterBoardGraphicManager.ChangeGraphicAll(masterBoard);
                        }
                        else
                        {
                            clientBoard.Move((MoveDirection)action.Parameter, out _);
                            clientBoardGraphicManager.ChangeGraphicAll(clientBoard);
                        }
                    }
                    break;
                case ActionType.BLOCK_SPAWN:
                    {
                        int x = action.Parameter / 16;
                        int y = action.Parameter % 16;

                        if (action.IsMaster)
                        {
                            masterBoard.SetValue(x, y, 2);
                            masterBoardGraphicManager.ChangeGraphicAll(masterBoard);
                            masterBoardGraphicManager.ShowBornParticleAt(x, y);
                        }
                        else
                        {
                            clientBoard.SetValue(x, y, 2);
                            clientBoardGraphicManager.ChangeGraphicAll(clientBoard);
                            clientBoardGraphicManager.ShowBornParticleAt(x, y);
                        }
                    }
                    break;
            }
        }

        #region Coroutine Functions

        IEnumerator UnloadMatchingScene()
        {
            yield return SceneManager.UnloadSceneAsync("OnlineMatching");
            Debug.Log("Unloaded Matching Scene Successfuly");
        }

        IEnumerator CountDown()
        {
            countDownGUI.SetActive(true);

            for(int i = 3; i >= 1; i--)
            {
                countDownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }

            Destroy(countDownGUI);
            gameSyncer.State = GameState.GAME_NOW;
        }

        #endregion

        #region Support Funcitons

        private void InvokeBlockMoved(MoveDirection direction)
        {
            var board = gameSyncer.PlayerStatus.IsMaster() ? masterBoard : clientBoard; 
            var clone = board.Clone() as BlockBoard;
            clone.Move(direction, out List<(int, int)> changed);
            if (changed.Count != 0 && !clone.Full)
            {
                gameSyncer.InvokeAction(ActionType.BLOCK_MOVED, (int)direction);

                var random_spawn_pos = clone.RandomSpawn();
                var random_spawn_pos_zip = random_spawn_pos.x * 16 + random_spawn_pos.y;
                gameSyncer.InvokeAction(ActionType.BLOCK_SPAWN, random_spawn_pos_zip);
            }
        }

        #endregion

        #region EventCallbacks

        private void OnGameStateChangedCallback(GameState state)
        {
            switch(state)
            {
                case GameState.GAME_NOW:
                    {
                        if (!gameSyncer.PlayerStatus.IsWatcher())
                        {
                            var random_spawn_pos_zip = Random.Range(1, 4 + 1) * 16 + Random.Range(1, 4 + 1);
                            gameSyncer.InvokeAction(ActionType.BLOCK_SPAWN, random_spawn_pos_zip);
                        }
                    }
                    break;
            }
        }

        #endregion
    }
}
