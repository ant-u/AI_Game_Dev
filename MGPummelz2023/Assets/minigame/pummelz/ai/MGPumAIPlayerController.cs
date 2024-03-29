﻿using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace mg.pummelz
{
    public abstract class MGPumAIPlayerController : MGPumPlayerController
    {
        private Thread aiThread;

        protected MGPumGameState state;
        protected MGPumGameStateHandler stateHandler;
        protected MGPumGameStateOracle stateOracle;

        protected bool threaded;

        private MGPummelz guiController;

        private List<Vector2Int> _directions = null;

        public MGPumAIPlayerController(int playerID, bool threaded) : base(playerID)
        {
            this.threaded = threaded;
        }

        public void setReportingGUIController(MGPummelz guiController)
        {
            this.guiController = guiController;
        }

        public override void allowCommand()
        {
            if(threaded)
            {
                aiThread = new Thread(new ThreadStart(this.calcCommandThreadMethod));
                aiThread.Start();
            }
            else
            {
                MGPumCommand command = calculateCommand();
                bool result = gameController.acceptCommand(command);
                if(!result)
                {
                    Debug.LogError("Falling back to end turn. AI tried illegal command: " + command);
                    gameController.acceptCommand(new MGPumEndTurnCommand(this.playerID));
                }
                
            }
        }

        public override void setInitialState(MGPumGameState state)
        {
            this.state = state;
            this.stateHandler = new MGPumGameStateHandler(state);
            this.stateOracle = new MGPumGameStateOracle(state);

            if(guiController != null)
            {
                guiController.setPlayerState(state);
            }
        }

        public override void updateState(List<MGPumGameEvent> newEvents)
        {
            foreach (MGPumGameEvent ev in newEvents)
            {
                MGPumGameEvent ksev = ev.deepCopy(state);
                stateHandler.applyEvent(ksev);
            }

            if (guiController != null)
            {
                guiController.visualizeEvents(newEvents, this.playerID);
            }
        }

        internal override void finishGame(MGPumGameResult result)
        {
            if(aiThread != null && aiThread.IsAlive)
            {
                aiThread.Abort();
            }
        }

        internal void calcCommandThreadMethod()
        {
            MGPumCommand command = null;

            try
            {
                command = calculateCommand();
            }
            catch(Exception e)
            {
                Debug.LogError("calculateCommand crashed " +  this.GetType());
                Debug.LogError(e);
            }

            bool result = false;
            if(command != null)
            {
                result = gameController.acceptCommandAsynchronous(command);
            }
            
            if (!result)
            {
                //retry after wait
                Thread.Sleep(500);
                if (command != null)
                {
                    result = gameController.acceptCommandAsynchronous(command);
                }
                if (!result)
                {
                    Debug.LogError("Gamecontroller didn't accept command from AI thread: " + command);
                    if (!result)
                    {
                        Debug.LogError("Falling back to end turn. AI " + this.GetType() + " tried illegal command: " + command);
                        if(command != null && this is MGPumStudentAIPlayerController)
                        {
                            Debug.LogError("Reason for rejection (use stateOracle.checkCommandDetailed): " + stateOracle.checkCommandDetailed(command, true).reason);
                        }

                        gameController.acceptCommandAsynchronous(new MGPumEndTurnCommand(this.playerID));
                    }
                }
            }

        }

        internal List<Vector2Int> getDirections()
        {
            if (_directions == null)
            {
                _directions = new List<Vector2Int>();
                _directions.Add(Vector2Int.left);
                _directions.Add(Vector2Int.right);
                _directions.Add(Vector2Int.up);
                _directions.Add(Vector2Int.down);
                _directions.Add(Vector2Int.left + Vector2Int.up);
                _directions.Add(Vector2Int.left + Vector2Int.down);
                _directions.Add(Vector2Int.right + Vector2Int.up);
                _directions.Add(Vector2Int.right + Vector2Int.down);
            }
            return _directions;
        }

        internal MGPumGameState tryMoveOnNewState(MGPumMoveCommand move)
        {
            MGPumGameState copiedState = state.deepCopy();

            MGPumAIPlayerDummyController dummy1 = new MGPumAIPlayerDummyController(0);
            MGPumAIPlayerDummyController dummy2 = new MGPumAIPlayerDummyController(1);

            MGPumGameController copiedController = new MGPumGameController(dummy1, dummy2, copiedState);

            bool result = copiedController.acceptCommand(move);
            if(!result)
            {
                Debug.LogWarning("AI considered illegal move.");
            }

            return copiedState;
        }

        internal abstract MGPumCommand calculateCommand();

     

    }
}
