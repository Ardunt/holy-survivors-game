﻿using System.Linq;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace HD
{
    public class MainSceneEventHandler : MonoBehaviour
    {
        // Existed UDP Gameobject and Prefab
        public GameObject udp;
        public GameObject udpPrefab;
        
        // For static calls
        public static MainSceneEventHandler instance;
        
        // Main UI Elements
        private GameObject mainUI;
        private GameObject lobbyUI;

        // Special UI Element
        public GameObject startButton;
        public TextMeshProUGUI countDownText;
        public GameObject roleButtonSection;
        public GameObject lockButton;

        // InputField Settings
        public InputField usernameInput;
        private string username;
        public InputField ipInput;
        private string ipText;

        void Start()
        {
            instance = this;
            mainUI = gameObject.transform.GetChild(0).gameObject;
            lobbyUI = gameObject.transform.GetChild(1).gameObject;
        }

        void Update()
        {
            // Checking UDP gameObject activity to set UI
            if(udp.activeSelf)
            {
                if(UDPChat.instance.connectionFailed)
                {
                    lobbyUI.SetActive(false);
                    mainUI.SetActive(true);

                    resetUDP();
                }
                else
                {
                    lobbyUI.SetActive(true);
                    mainUI.SetActive(false);
                }
            }
        }

        // Countdown Text Function
        private static string message = "Game starts in ";
        private static int second;

        internal static IEnumerator startCountDown()
        {
            for(second = 15; second > 0; second--)
            {
                if(UDPChat.instance.gameState != "stop")
                {
                    instance.countDownText.SetText( message + second.ToString() );
                    yield return new WaitForSeconds(1);
                }
                else
                {
                    instance.countDownText.SetText("");
                    second = 15;
                    break;
                }
            }

            if(second == 1)
            {
                SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            }
            
        }

        // OnClick Functions
        public void clientButtonFunc()
        {
            ipText = ipInput.text;
            username = usernameInput.text;

            if(checkUserName(username)){
                Globals.isServer = false;

                IPAddress ip = IPAddress.Parse(ipText);
                udp.GetComponent<UDPChat>().serverIp = ip;
                udp.SetActive(true);
            }
        }

        public void serverButtonFunc()
        {
            username = usernameInput.text;

            if(checkUserName(username)){
                Globals.isServer = true;
                udp.SetActive(true);
                UDPChat.instance.connectionFailed = false;
                
                startButton.SetActive(true);
            }     
        }
        
        public void exitButtonFunc()
        {
            // Set UDP settings as default
            if(!UDPChat.instance.isServer)
            {
                UDPChat.instance.clientDisconnected();
            }

            UDPChat.instance.connection.Close();
            resetUDP();
            
            // Set UI elements to show Main Menu
            LobbyList.clearLobbyList();
            
            // Set "Lock Button" default state
            UDPChat.instance.readyStatement = "N";
            roleButtonSection.SetActive(true);
            lockButton.transform.GetChild(0).gameObject.GetComponent<Text>().text = "Lock";

            lobbyUI.SetActive(false);
            startButton.SetActive(false);
            mainUI.SetActive(true);
        }
        
        //// Start Button Functions
        public void startButtonFunc()
        {
            if(instance.startButton.transform.GetChild(0).gameObject.GetComponent<Text>().text == "Start")
            {
                // check stateList if there is a unready player

                string[] checkReadyList = new string[UDPChat.instance.playerList.ToArray().Length];

                for(int i = 0; i < checkReadyList.Length; i++)
                {
                    checkReadyList[i] = LobbyList.instance.stateList[i].text;
                }
                
                if(!checkReadyList.Contains("N"))
                {
                    startGame();
                }

            }
            else
            {   
                stopGame();
            }
        }

        private void startGame()
        {
            instance.countDownText.gameObject.SetActive(true);

            instance.startButton.transform.GetChild(0).gameObject.GetComponent<Text>().text = "Stop";
            
            UDPChat.instance.gameState = "start";
            StartCoroutine(startCountDown());

            object[] gameMsg = new object[2]{ProtocolLabels.gameAction, "start"};

            string msg = MessageMaker.makeMessage(gameMsg);
            
            UDPChat.instance.Send(msg);
        }
       
        private void stopGame()
        {
            instance.countDownText.gameObject.SetActive(false);
            
            instance.startButton.transform.GetChild(0).gameObject.GetComponent<Text>().text = "Start";

            UDPChat.instance.gameState = "stop";

            object[] gameMsg = new object[2]{ ProtocolLabels.gameAction, "stop"};

            string msg = MessageMaker.makeMessage(gameMsg);
            
            UDPChat.instance.Send(msg);
        }

        //// Role Buttons Function
        public void roleButtonFunc(string roleName)
        {
            object[] roleInfo = new object[3]{ ProtocolLabels.roleSelected, 
                                               UDPChat.clientNo, 
                                               roleName
                                             };

            string msg = MessageMaker.makeMessage(roleInfo);
            
            UDPChat.instance.roleName = roleName;

            if(UDPChat.instance.isServer)
            {
                // Server must set roleName to lobby list by itself    
                LobbyList.setRolePref(roleName);

                UDPChat.instance.Send(msg);
            }
            else
            {
                UDPChat.instance.connection.Send(msg,
                                            new IPEndPoint(UDPChat.instance.serverIp, Globals.port));
            }
        }
        
        //// Lock button settings        
        public void lockButtonFunc()
        {
            string buttonName = lockButton.transform.GetChild(0).gameObject.GetComponent<Text>().text;
            
            //Set lock button's function according to its name
            if(buttonName == "Lock" && LobbyList.instance.imageList[UDPChat.clientNo].color != Color.white)
            {
                lockPref();
            }
            else if(buttonName == "Unlock" && !countDownText.gameObject.activeSelf) // add later 
            {
                unlockPref();
            }
        }

        private void lockPref()
        {
            UDPChat.instance.readyStatement = "R"; // "R" means "Ready"
            LobbyList.setReadyStatement("R", UDPChat.clientNo);
            
            
            
            object[] readyInfo = new object[3]{ProtocolLabels.clientReady, 
                                              UDPChat.clientNo, 
                                              "R"};
        
            string readyMsg = MessageMaker.makeMessage(readyInfo);

            UDPChat.instance.Send(readyMsg);

            roleButtonSection.SetActive(false);

            lockButton.transform.GetChild(0).gameObject.GetComponent<Text>().text = "Unlock";
        }

        private void unlockPref()
        {
            UDPChat.instance.readyStatement = "N"; // "N" means "Not Ready"
            LobbyList.setReadyStatement("N", UDPChat.clientNo);

            object[] unreadyInfo = new object[3]{ProtocolLabels.clientReady, 
                                                 UDPChat.clientNo, 
                                                 "N"};
        
            string unreadyMsg = MessageMaker.makeMessage(unreadyInfo);

            UDPChat.instance.Send(unreadyMsg);

            roleButtonSection.SetActive(true);

            lockButton.transform.GetChild(0).gameObject.GetComponent<Text>().text = "Lock";
        }

        // Common Functions in this script 
        
        //// To check username if it is approative or not
        //// Username cannot be space like " "   
        private bool isUsernameApproative;

        private bool checkUserName(string username)
        {
            int nameLength = username.Length;

            for(int i = 0; i < nameLength; i++)
            {
                if(username[i] == ' ')
                {
                    isUsernameApproative = false;
                }
                else
                {
                    isUsernameApproative = true;
                }
            }

            return isUsernameApproative;
        }

        //// set "udp" a new gameobject with "udpPrefab"
        private void resetUDP()
        {
            Destroy(udp);
            udp = Instantiate(udpPrefab);
            udp.name = "UDP GameObject";
        }
    }    
}