using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using DG.Tweening;
using System.Linq;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Best.HTTP.Shared;

public class SocketIOManager : MonoBehaviour
{
    [SerializeField]
    internal SlotBehaviour slotManager;

    [SerializeField]
    private UIManager uiManager;

    internal GameData initialData = null;
    internal UIData initUIData = null;
    internal GameData resultData = null;
    internal PlayerData playerdata = null;
    [SerializeField]
    internal List<string> bonusdata = null;
    //WebSocket currentSocket = null;
    internal bool isResultdone = false;
    // protected string nameSpace="game"; //BackendChanges
    protected string nameSpace = ""; //BackendChanges
    private Socket gameSocket; //BackendChanges

    private SocketManager manager;

    [SerializeField]
    internal JSHandler _jsManager;

    protected string SocketURI = null;
    // protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";
    protected string TestSocketURI = "http://localhost:5001/";
    [SerializeField] internal JSFunctCalls JSManager;
    [SerializeField]
    private string testToken;
    protected string gameID = "SL-WB";
    //protected string gameID = "";

    internal bool isLoaded = false;

    internal bool SetInit = false;

    private const int maxReconnectionAttempts = 6;
    private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);

    private void Awake()
    {
        //Debug.unityLogger.logEnabled = false;
        isLoaded = false;
        SetInit = false;

    }

    private void Start()
    {
        //OpenWebsocket();
        OpenSocket();
    }

    void ReceiveAuthToken(string jsonData)
    {
        Debug.Log("Received data: " + jsonData);

        // Parse the JSON data
        var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
        SocketURI = data.socketURL;
        myAuth = data.cookie;
        nameSpace = data.nameSpace;
        // Proceed with connecting to the server using myAuth and socketURL
    }

    string myAuth = null;

    private void OpenSocket()
    {
        //Create and setup SocketOptions
        SocketOptions options = new SocketOptions();
        options.ReconnectionAttempts = maxReconnectionAttempts;
        options.ReconnectionDelay = reconnectionDelay;
        options.Reconnection = true;
        options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket; //BackendChanges

        Application.ExternalCall("window.parent.postMessage", "authToken", "*");

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("authToken");
        StartCoroutine(WaitForAuthToken(options));
#else
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = testToken,
                gameId = gameID
            };
        };
        options.Auth = authFunction;
        // Proceed with connecting to the server
        SetupSocketManager(options);
#endif
    }

    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        // Wait until myAuth is not null
        while (myAuth == null)
        {
            Debug.Log("My Auth is null");
            yield return null;
        }
        while (SocketURI == null)
        {
            Debug.Log("My Socket is null");
            yield return null;
        }
        Debug.Log("My Auth is not null");
        // Once myAuth is set, configure the authFunction
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = myAuth,
                gameId = gameID
            };
        };
        options.Auth = authFunction;

        Debug.Log("Auth function configured with token: " + myAuth);

        // Proceed with connecting to the server
        SetupSocketManager(options);
    }

    private void SetupSocketManager(SocketOptions options)
    {
        // Create and setup SocketManager
#if UNITY_EDITOR
        this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
        if (string.IsNullOrEmpty(nameSpace))
        {  //BackendChanges Start
            gameSocket = this.manager.Socket;
        }
        else
        {
            print("nameSpace: " + nameSpace);
            gameSocket = this.manager.GetSocket("/" + nameSpace);
        }
        // Set subscriptions
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On<string>(SocketIOEventTypes.Disconnect, OnDisconnected);
        gameSocket.On<string>(SocketIOEventTypes.Error, OnError);
        gameSocket.On<string>("message", OnListenEvent);
        gameSocket.On<bool>("socketState", OnSocketState);
        gameSocket.On<string>("internalError", OnSocketError);
        gameSocket.On<string>("alert", OnSocketAlert);
        gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice); //BackendChanges Finish
                                                                     // Start connecting to the server
    }

    // Connected event handler implementation
    void OnConnected(ConnectResponse resp)
    {
        Debug.Log("Connected!");
        SendPing();
    }

    private void OnDisconnected(string response)
    {
        Debug.Log("Disconnected from the server");
        //if (maxReconnectionAttempts <= this.manager.ReconnectAttempts)
        //{
        StopAllCoroutines();
        uiManager.DisconnectionPopup(false);
        //}
        //else
        //{
        //    uiManager.DisconnectionPopup(false);
        //}
    }

    private void OnError(string response)
    {
        Debug.LogError("Error: " + response);
    }

    private void OnListenEvent(string data)
    {
        // Debug.Log("Received some_event with data: " + data);
        ParseResponse(data);
    }

    private void OnSocketState(bool state)
    {
        if (state)
        {
            Debug.Log("my state is " + state);
        }
        else
        {

        }
    }
    private void OnSocketError(string data)
    {
        Debug.Log("Received error with data: " + data);
    }
    private void OnSocketAlert(string data)
    {
//        Debug.Log("Received alert with data: " + data);
    }

    private void OnSocketOtherDevice(string data)
    {
        Debug.Log("Received Device Error with data: " + data);
        uiManager.ADfunction();
    }

    private void SendPing()
    {
        InvokeRepeating("AliveRequest", 0f, 3f);
    }

    private void AliveRequest()
    {
        SendDataWithNamespace("YES I AM ALIVE");
    }

    private void SendDataWithNamespace(string eventName, string json = null)
    {
        // Send the message
        if (gameSocket != null && gameSocket.IsOpen) //BackendChanges
        {
            if (json != null)
            {
                gameSocket.Emit(eventName, json);
                Debug.Log("JSON data sent: " + json);
            }
            else
            {
                gameSocket.Emit(eventName);
            }
        }
        else
        {
            Debug.LogWarning("Socket is not connected.");
        }
    }

    internal void ReactNativeCallOnFailedToConnect() //BackendChanges
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("onExit");
#endif
    }

    internal void CloseSocket()
    {
        SendDataWithNamespace("EXIT");
    }

    private void ParseResponse(string jsonObject)
    {
//        Debug.Log(jsonObject);
        Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);
       
        string id = myData.id;

        switch(id)
        {
            case "InitData":
                {
                   
                    initialData = myData.message.GameData;
                    initUIData = myData.message.UIData;
                    playerdata = myData.message.PlayerData;
                    bonusdata = myData.message.BonusData;
                    if (!SetInit)
                    {
                        Debug.Log(jsonObject);
                        Debug.Log(initialData.largeWheelFeature);
                        List<string> LinesString = ConvertListListIntToListString(initialData.linesApiData);
                        List<string> InitialReels = ConvertListOfListsToStrings(initialData.Reel);
                        InitialReels = RemoveQuotes(InitialReels);

                        PopulateSlotSocket(InitialReels, LinesString);
                        SetInit = true;
                    }
                    else
                    {
                        RefreshUI();
                    }
                    break;
                }
            case "ResultData":
                {
                     Debug.Log(jsonObject);
                    myData.message.GameData.FinalResultReel = ConvertListOfListsToStrings(myData.message.GameData.ResultReel);
                    myData.message.GameData.FinalsymbolsToEmit = TransformAndRemoveRecurring(myData.message.GameData.symbolsToEmit);
                    resultData = myData.message.GameData;
                    playerdata = myData.message.PlayerData;
                    isResultdone = true;
                    break;
                }
            case "ExitUser":
                {
                    if (gameSocket != null) //BackendChanges
                    {
                        Debug.Log("Dispose my Socket");
                        this.manager.Close();
                    }
                    Application.ExternalCall("window.parent.postMessage", "onExit", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("onExit");
#endif
                    break;
                }
        }
    }

    private void RefreshUI()
    {
        uiManager.InitialiseUIData(initUIData.AbtLogo.link, initUIData.AbtLogo.logoSprite, initUIData.ToULink, initUIData.PopLink, initUIData.paylines);
    }

    private void PopulateSlotSocket(List<string> slotPop, List<string> LineIds)
    {
        Debug.Log("shuffleran");
        slotManager.shuffleInitialMatrix();
        Debug.Log(LineIds.Count);
        for (int i = 0; i < LineIds.Count; i++)
        {
            
            slotManager.FetchLines(LineIds[i], i);
        }

        slotManager.SetInitialUI();

        isLoaded = true;
      //  Application.ExternalCall("window.parent.postMessage", "OnEnter", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("OnEnter");
#endif

    }

    internal void AccumulateResult(double currBet)
    {
        isResultdone = false;
        MessageData message = new MessageData();
        message.data = new BetData();
        message.data.currentBet = currBet;
        message.data.spins = 1;
        message.data.currentLines = 50;
        message.id = "SPIN";
        // Serialize message data to JSON
        string json = JsonUtility.ToJson(message);
        SendDataWithNamespace("message", json);
    }

    private List<string> RemoveQuotes(List<string> stringList)
    {
        for (int i = 0; i < stringList.Count; i++)
        {
            stringList[i] = stringList[i].Replace("\"", ""); // Remove inverted commas
        }
        return stringList;
    }

    private List<string> ConvertListListIntToListString(List<List<int>> listOfLists)
    {
        List<string> resultList = new List<string>();

        foreach (List<int> innerList in listOfLists)
        {
            // Convert each integer in the inner list to string
            List<string> stringList = new List<string>();
            foreach (int number in innerList)
            {
                stringList.Add(number.ToString());
            }

            // Join the string representation of integers with ","
            string joinedString = string.Join(",", stringList.ToArray()).Trim();
            resultList.Add(joinedString);
        }

        return resultList;
    }

    private List<string> ConvertListOfListsToStrings(List<List<string>> inputList)
    {
        List<string> outputList = new List<string>();

        foreach (List<string> row in inputList)
        {
            string concatenatedString = string.Join(",", row);
            outputList.Add(concatenatedString);
        }

        return outputList;
    }

    private List<string> TransformAndRemoveRecurring(List<List<string>> originalList)
    {
        // Flattened list
        List<string> flattenedList = new List<string>();
        foreach (List<string> sublist in originalList)
        {
            flattenedList.AddRange(sublist);
        }

        // Remove recurring elements
        HashSet<string> uniqueElements = new HashSet<string>(flattenedList);

        // Transformed list
        List<string> transformedList = new List<string>();
        foreach (string element in uniqueElements)
        {
            transformedList.Add(element.Replace(",", ""));
        }

        return transformedList;
    }
}

[Serializable]
public class BetData
{
    public double currentBet;
    public double currentLines;
    public double spins;
}

[Serializable]
public class AuthData
{
    public string GameID;
    //public double TotalLines;
}

[Serializable]
public class MessageData
{
    public BetData data;
    public string id;
}

[Serializable]
public class ExitData
{
    public string id;
}

[Serializable]
public class InitData
{
    public AuthData Data;
    public string id;
}

[Serializable]
public class AbtLogo
{
    public string logoSprite { get; set; }
    public string link { get; set; }
}

[Serializable]
public class GameData
{
    public List<List<string>> Reel { get; set; }
    public List<List<int>> linesApiData { get; set; }
    public List<double> Bets { get; set; }
    public List<int> smallWheelFeature { get; set; }
    public List<int> mediumWheelFeature { get; set; }
    public List<int> largeWheelFeature { get; set; }
    public bool canSwitchLines { get; set; }
    public bool isFreeSpin { get; set; }
    public int freeSpinCount;
    public bool isSmallWheelTriggered;
    public bool isMediumWheelTriggered;
    public bool isLargeWheelTriggered;
    public int indexToStop;
    public bool freeSpinAdded;
    public List<int> LinesCount { get; set; }
    public List<int> autoSpin { get; set; }
    public List<List<string>> ResultReel { get; set; }
    public List<int> linesToEmit { get; set; }
    public List<List<string>> symbolsToEmit { get; set; }
    public double WinAmout { get; set; }
    public FreeSpins freeSpins { get; set; }
    public List<string> FinalsymbolsToEmit { get; set; }
    public List<string> FinalResultReel { get; set; }
    public double jackpot { get; set; }
    public bool isBonus { get; set; }
    public int BonusStopIndex { get; set; }
}

[Serializable]
public class FreeSpins
{
    public int count { get; set; }
    public bool isNewAdded { get; set; }
}

[Serializable]
public class Message
{
    public GameData GameData { get; set; }
    public UIData UIData { get; set; }
    public PlayerData PlayerData { get; set; }
    public List<string> BonusData { get; set; }
}

[Serializable]
public class Root
{
    public string id { get; set; }
    public Message message { get; set; }
}

[Serializable]
public class UIData
{
    public Paylines paylines { get; set; }
    public List<string> spclSymbolTxt { get; set; }
    public AbtLogo AbtLogo { get; set; }
    public string ToULink { get; set; }
    public string PopLink { get; set; }
}

[Serializable]
public class Paylines
{
    public List<Symbol> symbols { get; set; }
}

[Serializable]
public class Symbol
{
    public int ID { get; set; }
    public string Name { get; set; }
    [JsonProperty("multiplier")]
    public object MultiplierObject { get; set; }

    // This property will hold the properly deserialized list of lists of integers
    [JsonIgnore]
    public List<List<double>> Multiplier { get; private set; }

    // Custom deserialization method to handle the conversion
    [OnDeserialized]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        // Handle the case where multiplier is an object (empty in JSON)
        if (MultiplierObject is JObject)
        {
            Multiplier = new List<List<double>>();
        }
        else
        {
            // Deserialize normally assuming it's an array of arrays
            Multiplier = JsonConvert.DeserializeObject<List<List<double>>>(MultiplierObject.ToString());
        }
    }
    public object defaultAmount { get; set; }
    public object symbolsCount { get; set; }
    public object increaseValue { get; set; }
    public object description { get; set; }
    public int freeSpin { get; set; }
}
[Serializable]
public class PlayerData
{
    public double Balance { get; set; }
    public double haveWon { get; set; }
    public double currentWining { get; set; }
}
[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace; 
}


