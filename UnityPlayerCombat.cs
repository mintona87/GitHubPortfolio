using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Drawing;
using Photon.Realtime;
using System;
using System.Runtime.InteropServices;
using static UnityEngine.GraphicsBuffer;

public class PlayerCombat : MonoBehaviourPunCallbacks
{
    public GameObject targetObj;
    public PlayerManager targetScript;

    PlayerManager playerManager;
    PlayerEffect playerEffect;

    public bool isPlayingAction;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        playerEffect = playerManager.playerEffect;
    }

    private void Start()
    {
        ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "countPlayerPlayingAction", 0 },
            { "SharedRandomNumber", 0 },
            { "isGameOver",  false}
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

    }


    public void SetDefaultTarget()
    {
        // To do : does not make sens 
        if (PhotonNetwork.LocalPlayer.GetPlayerNumber() == 0)
        {
            playerManager.gameController.MyPlayerObj = playerManager.gameController.playerList[0].gameObject;
            targetObj = playerManager.gameController.playerList[1].gameObject;

            playerManager.gameController.MyPlayerManager = playerManager.gameController.MyPlayerObj.GetComponent<PlayerManager>();
        }
        else
        {
            playerManager.gameController.MyPlayerObj = playerManager.gameController.playerList[1].gameObject;
            targetObj = playerManager.gameController.playerList[0].gameObject;

            playerManager.gameController.MyPlayerManager = playerManager.gameController.MyPlayerObj.GetComponent<PlayerManager>();
        }

        Debug.Log("playernumber " + PhotonNetwork.LocalPlayer.GetPlayerNumber());

        targetScript = targetObj.GetComponent<PlayerManager>();
    }

    public enum Actions
    {
        NoAction = 0,
        Attack = 1,
        Defend = 2,
        Charge = 3,
        Heal = 4,
    }

    public Actions choosenAction;

    void UpdateTurnAndUI()
    {
        Debug.Log("istargetstuned " + playerManager.playerCombat.targetScript.isStunned);

        if (playerManager.playerCombat.targetScript.isStunned)
        {
            playerManager.playerCombat.targetScript.gameController.HandlePlayerTurn();
        }
        else
        {
            playerManager.gameController.HandlePlayerTurn();
        }
    }


    //gets a player different than local player
    public Player GetOtherPlayer()
    {
        Player otherPlayer = null;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player != PhotonNetwork.LocalPlayer)
            {
                otherPlayer = player;
                break;
            }
        }
        return otherPlayer;
    }


    private void Update()
    {
        //Debug.Log(" num " + PhotonNetwork.LocalPlayer.GetPlayerNumber()+"turncharge " + playerManager.TurnsSinceCharge +" hascharged "+playerManager.HasCharged);
        //Debug.Log("isplayingaction" + isPlayingAction + " num " + PhotonNetwork.LocalPlayer.GetPlayerNumber() + "manastun " + playerManager.canPlay);
        if (isPlayingAction == true || (bool)PhotonNetwork.LocalPlayer.CustomProperties["DidFinishChoosingAction"] == true)
        {
            DisableButtonUI();
        }
    }

    //Set a random number only on the master, so both players share the same number
    private void SetSharedRandomNumber()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            int rnd = UnityEngine.Random.Range(1, 100);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "SharedRandomNumber", rnd } });
        }
    }


    // called when both players finish to selecte an action. This part handle the order in which players are playing the actions.
    IEnumerator CheckIfShouldPlayAction()
    {
        const float waitTime = 3.0f;
        isPlayingAction = true;
        targetScript.playerCombat.isPlayingAction = true;


        // wait if no actions are selected (Unless there is a buf it should never be called)
        if (PhotonNetwork.LocalPlayer.IsLocal)
        {
            while (choosenAction == Actions.NoAction)
            {
                yield return null;
            }
        }

        //Reset DidFinishChoosingAction for all players
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            player.CustomProperties["DidFinishChoosingAction"] = false;
        }

        int sharedRandomNumber = (int)PhotonNetwork.CurrentRoom.CustomProperties["SharedRandomNumber"];
        Debug.Log("SharedRandomNumber: " + sharedRandomNumber);

        // set the variable that indicates that the player is not choosing action anymore but he is playing the action that he choose
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "isPlayingAction", true } });



        int localSpd = Convert.ToInt32(PhotonNetwork.LocalPlayer.CustomProperties["SPD"]);
        int otherSpd = Convert.ToInt32(GetOtherPlayer().CustomProperties["SPD"]);

        Debug.Log("localspd " + localSpd + "other " + otherSpd);

        if (localSpd > otherSpd)
        {
            if (PhotonNetwork.LocalPlayer.IsLocal)
            {
                CheckStunAndChooseAction();
            }
        }
        else if (localSpd == otherSpd)
        {
            //choose who plays first randomely
            if (sharedRandomNumber > 50)
            {
                if (PhotonNetwork.LocalPlayer.GetPlayerNumber() == 0)
                {
                    CheckStunAndChooseAction();
                }
                else
                {
                    yield return new WaitForSeconds(waitTime);
                    CheckStunAndChooseAction();
                }
            }
            else
            {
                if (PhotonNetwork.LocalPlayer.GetPlayerNumber() == 1)
                {
                    CheckStunAndChooseAction();
                }
                else
                {
                    yield return new WaitForSeconds(waitTime);
                    CheckStunAndChooseAction();
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(waitTime);
            if (PhotonNetwork.LocalPlayer.IsLocal)
            {
                CheckStunAndChooseAction();
            }
        }

        // reset variables for the next turn
        if (playerManager.isItMyPlayer)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "isPlayingAction", false } });
            Debug.Log("playingactionfalsecalled");
        }
        isPlayingAction = false;
        targetScript.playerCombat.isPlayingAction = false;
        PhotonNetwork.CurrentRoom.CustomProperties["SharedRandomNumber"] = 0;
        choosenAction = Actions.NoAction;

        if ((bool)PhotonNetwork.LocalPlayer.CustomProperties["isDefending"])
        {
            playerManager.isDefendingTurnIndex++;
        }

        yield return null;
    }

    void CheckStunAndChooseAction()
    {
        if (!playerManager.isStunned)
        {
            ChooseAction();
        }
        else
        {
            int localPlayerViewID = playerManager.pv.ViewID;
            int targetPlayerViewID = targetScript.pv.ViewID;
            string targetName = "localPlayer";

            playerManager.pv.RPC("ShowActionRPC", RpcTarget.AllBuffered, playerManager.playerUI.playerUsernameText.text + " is stunned!", localPlayerViewID, targetPlayerViewID, targetName);

            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "isPlayerStun", "notStun" } });
        }
    }

    public void DisableButtonUI()
    {

        foreach (PlayerManager playerManager in FindObjectsOfType<PlayerManager>())
        {
            Debug.Log("shouldbedisabled" + playerManager.gameObject.name);
            playerManager.playerUI.playerAttackButton.interactable = false;
            playerManager.playerUI.playerHealButton.interactable = false;
            playerManager.playerUI.playerDefendButton.interactable = false;
            playerManager.playerUI.playerChargeButton.interactable = false;
        }
    }
    public void SetButtonUIAccordingToCanPlay()
    {
        foreach (PlayerManager playerManager in FindObjectsOfType<PlayerManager>())
        {
            playerManager.playerUI.playerAttackButton.interactable = playerManager.canPlay;
            playerManager.playerUI.playerHealButton.interactable = playerManager.canPlay;
            playerManager.playerUI.playerDefendButton.interactable = playerManager.canPlay;
            playerManager.playerUI.playerChargeButton.interactable = playerManager.canPlay && playerManager.TurnsSinceCharge >= 6;

            if (playerManager.isDefendingTurnIndex >= 2)
            {
                playerManager.SetIsDefending(PhotonNetwork.LocalPlayer, false);
                playerManager.isDefendingTurnIndex = 0;
            }

           
            if (playerManager.TurnsSinceCharge >= 6)
            {
                playerManager.ResetCharge();
            }

        }
    }
    void ChooseAction()
    {
        switch (choosenAction)
        {
            case Actions.Attack:
                Attack();
                break;
            case Actions.Defend:
                Defend();
                break;
            case Actions.Charge:
                Charge();
                break;
            case Actions.Heal:
                Heal();
                break;
            case Actions.NoAction:
                Debug.LogError("No action was selected");
                break;
        }
    }


    #region custom prop updated callback

    // called after "PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { .... "is called
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        Debug.Log("Callback called");

        // Check if the "DidFinishChoosingAction" property has been updated
        if (changedProps.ContainsKey("DidFinishChoosingAction"))
        {
            int countTrue = 0;
            // Iterate through all players in the room
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                // Check if the player has the custom property "DidFinishChoosingAction"
                if (player.CustomProperties.ContainsKey("DidFinishChoosingAction"))
                {
                    // Get the updated value
                    bool didFinishChoosingAction = (bool)player.CustomProperties["DidFinishChoosingAction"];

                    // Perform any required action based on the value
                    Debug.Log($"callbackdisfinish {player.NickName} has DidFinishChoosingAction set to: {didFinishChoosingAction}" + " trucount " + countTrue);
                    if (didFinishChoosingAction)
                    {
                        countTrue++;
                        Debug.Log("countrue " + countTrue);
                    }
                }
            }
            if (countTrue == 2)
            {
                SetSharedRandomNumber();
                StartCoroutine(WaitForSharedRandomNumberAndStartCheck());
            }
            if (countTrue == 1)
            {
                UpdateTurnAndUI();
            }
        }
        else if (changedProps.ContainsKey("isPlayingAction"))
        {


            // Iterate through all players in the room

            // Check if the player has the custom property "DidFinishChoosingAction"
            if (targetPlayer.CustomProperties.ContainsKey("isPlayingAction"))
            {
                // Get the updated value
                if (playerManager.isItMyPlayer)
                {
                    bool isPlayingAction = (bool)targetPlayer.CustomProperties["isPlayingAction"];

                    Debug.Log("customprop isPlayingAction " + isPlayingAction + " objname " + gameObject.name);

                    // Perform any required action based on the value

                    Debug.Log($"callbackPlayingaction {targetPlayer.NickName} has isPlayingAction set to: {isPlayingAction}");
                    if (isPlayingAction == false)
                    {
                        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("countPlayerPlayingAction", out object value))
                        {
                            int countPlayerPlayingAction = (int)value;
                            countPlayerPlayingAction++;
                            Debug.Log("wtfincrease " + countPlayerPlayingAction + "value " + (int)value);

                            ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable
                            {
                                { "countPlayerPlayingAction", countPlayerPlayingAction }
                            };
                            Debug.Log("countPlayerPlayingAction " + countPlayerPlayingAction + " num " + targetPlayer.GetPlayerNumber());
                            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
                        }
                    }
                }
            }
        }
        else if (changedProps.ContainsKey("isPlayerStun"))
        {
            // Iterate through all players in the room
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                // Check if the player has the custom property "isPlayerStun"
                if (player.CustomProperties.ContainsKey("isPlayerStun"))
                {
                    bool isPlayerStun;

                    // Get the updated value
                    if (player.CustomProperties["isPlayerStun"].ToString() == "stun")
                    {
                        isPlayerStun = true;
                    }
                    else
                    {
                        isPlayerStun = false;
                    }

                    Debug.Log("stunplayerid " + player.GetPlayerNumber() + " stun " + isPlayerStun);
                    if (player.GetPlayerNumber() == PhotonNetwork.LocalPlayer.GetPlayerNumber())
                    {
                        playerManager.isStunned = isPlayerStun;
                    }
                }
            }
        }
    }

    IEnumerator WaitForSharedRandomNumberAndStartCheck()
    {
        Debug.Log("Waiting for SharedRandomNumber");
        while (Convert.ToInt32(PhotonNetwork.CurrentRoom.CustomProperties["SharedRandomNumber"]) == 0)
        {
            yield return null;
        }

        Debug.Log("ShouldStartCheck");
        StartCoroutine(CheckIfShouldPlayAction());
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        // Handle the changed properties
        if (propertiesThatChanged.TryGetValue("countPlayerPlayingAction", out object value))
        {
            int getCountPlayerPlayingAction = (int)value;
            Debug.Log("roomvalue update " + getCountPlayerPlayingAction);
            if (getCountPlayerPlayingAction == 2)
            {
                isPlayingAction = false;
                targetScript.playerCombat.isPlayingAction = false;
                Debug.Log("finish playing action ");
                SetButtonUIAccordingToCanPlay();

                ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable
                {
                    { "countPlayerPlayingAction", 0 }
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
            }
        }
        else if (propertiesThatChanged.TryGetValue("isGameOver", out object gameoverValue))
        {
            bool isGameOver = (bool)gameoverValue;
            playerManager.gameController.gameOverManager.isGameOver = isGameOver;
            Debug.Log("roomvalue update gameover " + isGameOver);

        }
    }


    #endregion

    #region Attack
    public void OnPlayerAttackButtonClicked()
    {
        if (playerManager.HasLost()) return;
        choosenAction = Actions.Attack;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "DidFinishChoosingAction", true } });
        UpdateTurnAndUI();
    }

    void Attack()
    {
        if (playerManager.HasLost()) return;

        int damage = playerManager.Attack();

        // to do set target

        int localPlayerViewID = playerManager.pv.ViewID;
        int targetPlayerViewID = targetScript.pv.ViewID;
        string targetName = "target";
        Debug.Log("istargetdefending? " + targetScript.isDefending);
        if (!targetScript.Dodge()) // Check if Player 2 dodged the attack
        {
            targetScript.ChangeHP(-damage);
            Debug.Log("changeHPDefending " + (bool)GetOtherPlayer().CustomProperties["isDefending"]);
            if ((bool)GetOtherPlayer().CustomProperties["isDefending"])
            {
                targetScript.SetIsDefending(GetOtherPlayer() ,false);
                targetScript.isDefendingTurnIndex = 0;
            }
            playerManager.pv.RPC("ShowActionRPC", RpcTarget.AllBuffered, targetScript.playerUI.playerUsernameText.text + $" dealt {damage} damage!", localPlayerViewID, targetPlayerViewID, targetName);
        }
        else
        {
            playerManager.pv.RPC("ShowActionRPC", RpcTarget.AllBuffered, playerManager.playerUI.playerUsernameText.text + " missed the attack!", localPlayerViewID, targetPlayerViewID, targetName);
        }

        playerManager.TurnsSinceCharge++;
        Debug.Log("should charge " + playerManager.TurnsSinceCharge);

        StartCoroutine(playerEffect.PlaySwordSlashEffect());
    }
    #endregion


    #region Heal
    public void OnPlayerHealButtonClicked()
    {
        if (playerManager.HasLost()) return;
        choosenAction = Actions.Heal;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "DidFinishChoosingAction", true } });
        UpdateTurnAndUI();

    }

    void Heal()
    {
        int healAmount = playerManager.Heal();
        playerManager.ChangeHP(healAmount);
        if (playerManager.HP > playerManager.MaxHP)
        {
            playerManager.HP = playerManager.MaxHP;
        }
        playerManager.TurnsSinceCharge++;
        Debug.Log("should charge " + playerManager.TurnsSinceCharge);
        StartCoroutine(playerEffect.PlayHealEffect());

        int localPlayerViewID = playerManager.pv.ViewID;
        int targetPlayerViewID = targetScript.pv.ViewID;
        string targetName = "localPlayer";

        playerManager.pv.RPC("ShowActionRPC", RpcTarget.AllBuffered, playerManager.playerUI.playerUsernameText.text + $" healed for {healAmount}!", localPlayerViewID, targetPlayerViewID, targetName);

    }

    #endregion

    #region Defend
    public void OnPlayerDefendButtonClicked()
    {
        if (playerManager.HasLost()) return;
        choosenAction = Actions.Defend;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "DidFinishChoosingAction", true } });
        UpdateTurnAndUI();
    }

    void Defend()
    {
        playerManager.SetIsDefending(PhotonNetwork.LocalPlayer,true);
        playerManager.TurnsSinceCharge++;

        Debug.Log("Should defend");
        StartCoroutine(playerEffect.PlayDefendEffect());
    }
    #endregion

    #region Charge
    public void OnPlayerChargeButtonClicked()
    {
        if (playerManager.HasLost()) return;
        choosenAction = Actions.Charge;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "DidFinishChoosingAction", true } });
        UpdateTurnAndUI();
    }

    void Charge()
    {
        if (playerManager.HasCharged)
        {
            // Show error message that Charge is on cooldown
            return;
        }

        int damage = playerManager.Charge(targetScript);

        targetScript.ChangeHP(-damage);
        Debug.Log("changeHPDefending " + (bool)GetOtherPlayer().CustomProperties["isDefending"]);
        if ((bool)GetOtherPlayer().CustomProperties["isDefending"])
        {
            targetScript.SetIsDefending(GetOtherPlayer(), false);
            targetScript.isDefendingTurnIndex = 0;
        }
        playerManager.TurnsSinceCharge = 0; // Reset the counter
        Debug.Log("should charge " + playerManager.TurnsSinceCharge);

        StartCoroutine(playerEffect.PlaySwordSlashEffect());


        int localPlayerViewID = playerManager.pv.ViewID;
        int targetPlayerViewID = targetScript.pv.ViewID;
        string targetName = "target";
        
        playerManager.pv.RPC("ShowActionRPC", RpcTarget.AllBuffered, targetScript.playerUI.playerUsernameText.text + $" dealt {damage} damage!", localPlayerViewID, targetPlayerViewID, targetName);
    }
    #endregion

    [PunRPC]
    void ShowActionRPC(string text, int localPlayerViewID, int targetPlayerViewID, string targetName)
    {

        PhotonView localPlayerPV = PhotonView.Find(localPlayerViewID);
        PhotonView targetPlayerPV = PhotonView.Find(targetPlayerViewID);
        PlayerManager localPlayerManager = localPlayerPV.GetComponent<PlayerManager>();
        PlayerManager targetPlayerManager = targetPlayerPV.GetComponent<PlayerManager>();

        switch (targetName)
        {
            case "target":
                StartCoroutine(ShowActionText(text, targetPlayerManager.playerUI.ActionText));
                break;
            case "localPlayer":
                StartCoroutine(ShowActionText(text, localPlayerManager.playerUI.ActionText));
                break;
        }
    }


    // Coroutine to show action text and fade it away
    public IEnumerator ShowActionText(string text, TextMeshProUGUI textComponent)
    {

        textComponent.gameObject.SetActive(true);

        float duration = 1.5f; // How long the text should stay visible
        float fadeDuration = 0.5f; // How long the fade in/out should take

        // Set the text and alpha value to 0
        textComponent.text = text;
        textComponent.canvasRenderer.SetAlpha(0f);

        // Fade in
        textComponent.CrossFadeAlpha(1f, fadeDuration, false);

        // Wait for the duration
        yield return new WaitForSeconds(duration);

        // Fade out
        textComponent.CrossFadeAlpha(0f, fadeDuration, false);
        textComponent.gameObject.SetActive(false);
    }
}
