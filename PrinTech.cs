using BerryLoaderNS;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;

namespace PrinTech
{
    class Main : BerryLoaderMod
    {
        public override void Init()
        {
            SetCardBagHelper.BasicIdea += ", blueprint_PrinTech_coal";
            SetCardBagHelper.BasicBuildingIdea += ", blueprint_PrinTech_furnace";
            SetCardBagHelper.AdvancedBuildingIdea += ", blueprint_PrinTech_arm";
            Harmony.CreateAndPatchAll(typeof(Main));
        }
        //Change 3.5f to whatever world size you want to be the maximum
        /*[HarmonyPatch(typeof(WorldManager), "DetermineTargetWorldSize")]
        [HarmonyPrefix]
        public static bool WorldLimitPatch(ref float __result)
        {
            __result = Mathf.Clamp((float)WorldManager.instance.CardCapIncrease() * 0.03f, 0.15f, 3.5f);
            return false;
        }*/

        //Allows us to make the card appear slightly off the ground when we set isHovering to true in CustomCardData
        //just as if we were dragging it ourselves
        [HarmonyPatch(typeof(Draggable), "IsHovered", MethodType.Getter)]
        [HarmonyPostfix]
        public static void HoverablePatch(ref bool __result, ref Draggable __instance)
        {
            __result = __result || __instance.GetComponent<CustomCardData>().isHovering;
        }

        //Adds CustomCardData to every GameCard at the beginning of the game
        [HarmonyPatch(typeof(Draggable), "Awake", MethodType.Normal)]
        [HarmonyPrefix]
        public static bool CustomDataPatch(ref GameCard __instance)
        {
            __instance.gameObject.AddComponent<CustomCardData>();
            return true;
        }

        //When a card lands on another card after we call SendIt, we set isGoingToCard to false, which means that it can be picked up again
        //And we set the card it lands on's cardIncoming to false, which means that it can accept more cards
        [HarmonyPatch(typeof(GameCard), "Bounce")]
        [HarmonyPostfix]
        public static void ChangeIsGoingToPatch(ref GameCard __instance)
        {
            __instance.GetComponent<CustomCardData>().isGoingToCard = false;
            if (__instance.GetRootCard() != null)
            {
                __instance.GetRootCard().GetComponent<CustomCardData>().cardIncoming = false;
            }
        }

        //We remove the card from hoveringCards because it has just been clicked
        //We also add it to cardsToBeAdded, which means that the arm will check every so often to see if it can grab it again, if anything has changed (for example we stopped dragging it)
        [HarmonyPatch(typeof(Draggable), "StartDragging", MethodType.Normal)]
        [HarmonyPrefix]
        public static bool DraggingPatch(ref Draggable __instance)
        {
            CustomCardData customData = __instance.GetComponent<CustomCardData>();
            if (customData.arm != null)
            {
                GameCard card = __instance.GetComponent<GameCard>();
                CardData cardData = card.CardData;
                Arm.UsefulCardData usefulData = new Arm.UsefulCardData(card, customData);
                customData.arm.cardsToBeAdded.Add(usefulData);
                customData.arm.RemoveCard(card, cardData, customData);
            }
            return true;
        }

        //All CanHaveCard patches basically make the card not accept any other cards if it is hovering
        //There is a better way to do this, using the InteractionAPI, but this was made before that existed and i'm too lazy to fix it
        //Patches start here
        [HarmonyPatch(typeof(Resource), "CanHaveCard")]
        [HarmonyPostfix]
        protected static void ResourceStackPatch(ref bool __result, ref Resource __instance)
        {
            if (__instance.MyGameCard.GetComponent<CustomCardData>().isHovering)
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Wood), "CanHaveCard")]
        [HarmonyPostfix]
        protected static void WoodStackPatch(ref bool __result, ref Resource __instance)
        {
            if (__instance.MyGameCard.GetComponent<CustomCardData>().isHovering)
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Stone), "CanHaveCard")]
        [HarmonyPostfix]
        protected static void StoneStackPatch(ref bool __result, ref Resource __instance)
        {
            if (__instance.MyGameCard.GetComponent<CustomCardData>().isHovering)
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Food), "CanHaveCard")]
        [HarmonyPostfix]
        protected static void FoodStackPatch(ref bool __result, ref Resource __instance)
        {
            if (__instance.MyGameCard.GetComponent<CustomCardData>().isHovering)
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Poop), "CanHaveCard")]
        [HarmonyPostfix]
        protected static void PoopStackPatch(ref bool __result, ref Resource __instance)
        {
            if (__instance.MyGameCard.GetComponent<CustomCardData>().isHovering)
            {
                __result = false;
            }
        }
        //Patches end here

        //This removes all hovering cards from a GameCard before we sell it if it is an arm
        [HarmonyPatch(typeof(WorldManager), "SellCard")]
        [HarmonyPrefix]
        public static bool SellArmPatch(ref GameCard card)
        {
            if (card.name.Substring(0, 3) == "Arm")
            {
                card.GetComponentInChildren<Arm>().RemoveAllCards();
            }
            return true;
        }

        //This disables all arms(not fully) while villagers are being fed because arms can hold food, and if they aren't disabled, the villagers can't use it and the game gets stuck
        [HarmonyPatch(typeof(WorldManager), "EndOfMonth")]
        [HarmonyPrefix]
        public static bool EndOfMonthStopPatch()
        {
            foreach (GameCard card in WorldManager.instance.AllCards)
            {
                if (card.name.Substring(0, 3) == "Arm")
                {
                    Arm arm = (Arm)card.CardData;
                    arm.isEnabled = false;
                }
            }
            return true;
        }

        //This enables all arms again after villagers are fed
        [HarmonyPatch(typeof(WorldManager), "Save")]
        [HarmonyPrefix]
        public static bool EndOfMonthStartPatch()
        {
            foreach (GameCard card in WorldManager.instance.AllCards)
            {
                if (card.name.Substring(0, 3) == "Arm")
                {
                    Arm arm = (Arm)card.CardData;
                    arm.isEnabled = true;
                }
            }
            return true;
        }
    }

    //I basically just copy pasted the code from the brickyard in order to make the furnace and changed the id values
    //Could have been done in a better way, but 1. I could't be asked and 2. I only did this once and then forgot about it
    public class Furnace : Building
    {
        //Sets isBuilding to true so that the card isn't classified as a Structure and turns pink
        protected override void Awake()
        {
            base.Awake();
            this.IsBuilding = true;
        }
        //Sets which cards are allowed to be stacked on top of it
        protected override bool CanHaveCard(CardData otherCard) => otherCard.Id == "wood" || otherCard.Id == "iron_bar" || otherCard.Id == "PrinTech_coal";
        public override void Update()
        {
            if (this.ChildrenMatchingPredicateCount((System.Predicate<CardData>)(c => c.Id == "wood")) >= 3)
                this.MyGameCard.StartTimer(10f, new TimerAction(this.CompleteMakingCoal), "Burning wood", this.GetActionId("CompleteMakingCoal"));
            else
                this.MyGameCard.CancelTimer(this.GetActionId("CompleteMakingCoal"));
            if (this.ChildrenMatchingPredicateCount((System.Predicate<CardData>)(c => c.Id == "PrinTech_coal")) >= 1 && this.ChildrenMatchingPredicateCount((System.Predicate<CardData>)(c => c.Id == "iron_bar")) >= 2)
                this.MyGameCard.StartTimer(10f, new TimerAction(this.CompleteMakingSteel), "Combining weak, puny metal into something that's worthy of leadership", this.GetActionId("CompleteMakingSteel"));
            else
                this.MyGameCard.CancelTimer(this.GetActionId("CompleteMakingSteel"));
            base.Update();
        }
        public override bool CanHaveCardsWhileHasStatus() => true;
        [TimedAction("complete_making_coal")]
        public void CompleteMakingCoal()
        {
            this.DestroyChildrenMatchingPredicateAndRestack((System.Predicate<CardData>)(c => c.Id == "wood"), 3);
            for (int count = 0; count < 2; count++)
            {
                CardData card = WorldManager.instance.CreateCard(this.transform.position, "PrinTech_coal", false, false);
                WorldManager.instance.StackSend(card.MyGameCard, this.MyGameCard);
            }
        }
        [TimedAction("complete_making_steel")]
        public void CompleteMakingSteel()
        {
            this.DestroyChildrenMatchingPredicateAndRestack((System.Predicate<CardData>)(c => c.Id == "PrinTech_coal"), 1);
            this.DestroyChildrenMatchingPredicateAndRestack((System.Predicate<CardData>)(c => c.Id == "iron_bar"), 2);
            for (int count = 0; count < 1; count++)
            {
                CardData card = WorldManager.instance.CreateCard(this.transform.position, "PrinTech_steel", false, false);
                WorldManager.instance.StackSend(card.MyGameCard, this.MyGameCard);
            }
        }
    }
    public class DogHouse : Building
    {
        protected override void Awake()
        {
            base.Awake();
            this.IsBuilding = true;
        }
    }
    public class Arm : Building
    {
        SphereCollider range;
        Rigidbody rb;

        //These three lists are very important. The hoveringCards are the cards spinning around the arm
        public List<UsefulCardData> hoveringCards = new List<UsefulCardData>();

        //BuildingsInRange are cards that are in range and are buildings, which means that hovering cards can be sent to them
        public List<UsefulCardData> buildingsInRange = new List<UsefulCardData>();

        //cardsToBeAdded are cards that couldn't be added for a reason that could change in the future, and the arm should check every 0.1 second to see if it should add it
        public List<UsefulCardData> cardsToBeAdded = new List<UsefulCardData>();

        //Keep track of all gameobjects inside the range
        List<GameObject> gameObjects = new List<GameObject>();
        public float dragRadius = 0.85f;
        float angle = 0f;
        float baseAngle = 0f;
        float separationAngle = 0f;
        public static float pi = 3.14159f;
        public static int current_id_num = 0;
        public static int times = 0;
        private float timeToCheck = 0f;
        private float checkDelay = 0.2f;
        public bool isEnabled = true;

        protected override void Awake()
        {
            //base.Awake() does what every other card would do on awake
            base.Awake();

            //These two lines set the Arm's name to Arm 0, Arm 1 and so on as
            GetComponentInParent<GameCard>().gameObject.name = "Arm " + current_id_num.ToString();
            current_id_num++;

            //Makes the card a building and turns it pink
            IsBuilding = true;

            //Ads a sphere collider to it. This collider is how i check if a card is in range or not. If it has entered the collider and hasn't left yet, it's in range
            range = gameObject.AddComponent<SphereCollider>();
            rb = gameObject.AddComponent<Rigidbody>();
            rb = gameObject.GetComponent<Rigidbody>();
            rb.useGravity = false;
            range.isTrigger = true;
            range.radius = 1.3f;
            range.enabled = true;
        }
        public override void Update()
        {
            //base.Update() does what every other card would do on awake
            base.Update();

            //If the arm isn't enabled, don't spin the cards or check for new cards in range
            if (!isEnabled) return;

            //Spin the cardss
            for (int count = 0; count < hoveringCards.Count; count++)
            {
                angle = separationAngle * count + baseAngle;
                Vector3 offset = new Vector3(dragRadius * Mathf.Sin(angle), 0f, dragRadius * Mathf.Cos(angle));
                hoveringCards[count].card.TargetPosition = gameObject.transform.position + offset;
            }

            //This is how I check every 0.1 second
            if (Time.time > timeToCheck)
            {
                timeToCheck = Time.time + checkDelay;
                CheckForNewCards();
                CheckForAvailableCards();
            }
            baseAngle += 0.2f * Time.deltaTime;

        }
        public void OnTriggerEnter(Collider collider)
        {
            //If the Arm isn't enabled, don't accept new cards if they get in range. This only happens when villagers are being fed
            if (!isEnabled) return;

            //Each card has two colliders, one trigger and one normal one. Checking only one of them is easier
            if (collider.isTrigger == false) return;

            //If the card is an arm, don't add it
            if (collider.gameObject.name.Substring(0, 3) == "Arm") return;

            //Check if what the arm collided with is a card
            GameCard card = collider.GetComponent<GameCard>();
            if (card == null) return;
            CardData cardData = card.CardData;
            if (cardData == null) return;
            if (cardData.Id == "gold") return;
            if (cardData.Id == "corpse") return;
            CustomCardData customData = card.GetComponent<CustomCardData>();
            if (customData == null) return;

            //If this card already exists in gameObjects, don't add it
            foreach (GameObject gObject in gameObjects)
            {
                if (gObject == card.gameObject)
                {
                    return;
                }
            }

            //Try to add the card. If the card isn't added for any reason, reason won't be null
            RejectReason? reason = AddCard(card, cardData, customData);

            //If the reject reason isn't null, and is a reason that can change in the future, add it to cardsToBeAdded
            if (reason == RejectReason.IsUsefulInCurrentStack || reason == RejectReason.TooManyHoveringCards || reason == RejectReason.IsBeingDragged || reason == RejectReason.IsGoingToCard)
            {
                cardsToBeAdded.Add(new UsefulCardData(card, customData));
            }
        }
        public void OnTriggerExit(Collider collider)
        {
            //If the card is an arm, don't bother
            if (collider.gameObject.name.Substring(0, 3) == "Arm") return;

            //Check if what the arm collided with is a card
            GameCard card = collider.GetComponent<GameCard>();
            if (card == null) return;
            CardData cardData = card.CardData;
            if (cardData == null) return;
            CustomCardData customData = card.GetComponent<CustomCardData>();
            if (customData == null) return;
            if (customData.isHovering) return;

            //Remove the card as it is out of range
            RemoveCard(card, cardData, customData);

            //Remove the card from cardsToBeAdded too
            foreach (UsefulCardData usefulData in cardsToBeAdded)
            {
                if (usefulData.card.gameObject == card.gameObject)
                {
                    cardsToBeAdded.Remove(usefulData);
                    break;
                }
            }
        }

        //This whole method checks if a building that is in range of the arm could use a resource that is hovering around the arm and sends it to the building
        public void CheckForAvailableCards()
        {
            for (int count = 0; count < buildingsInRange.Count; count++)
            {
                UsefulCardData usefulBuildingData = buildingsInRange[count];
                GameCard buildingCard = usefulBuildingData.card;
                CardData buildingData = buildingCard.CardData;
                for (int scount = 0; scount < hoveringCards.Count; scount++)
                {
                    UsefulCardData usefulHoveringData = hoveringCards[scount];
                    GameCard hoveringCard = usefulHoveringData.card;
                    CardData hoveringData = hoveringCard.CardData;
                    //Debug.Log("Should send " + buildingData.name + " to " + hoveringData.name + ": " + ShouldSendCard(buildingData, hoveringData));
                    if (ShouldSendCard(buildingData, hoveringData))
                    {
                        CustomCardData customData = usefulHoveringData.customData;
                        customData.isGoingToCard = true;
                        usefulBuildingData.customData.cardIncoming = true;
                        RemoveCard(hoveringCard, hoveringData, customData);
                        cardsToBeAdded.Add(usefulHoveringData);
                        Vector3 vector3_2 = (buildingCard.transform.position - hoveringCard.transform.position) with
                        {
                            y = 0.0f
                        };
                        Vector3 vector3_1 = new Vector3(vector3_2.x * 4f, 7f, vector3_2.z * 4f);
                        hoveringCard.BounceTarget = buildingCard;
                        hoveringCard.SendIt();
                        hoveringCard.Velocity = new Vector3?(vector3_1);
                        break;
                    }
                }
            }
        }
        public void CheckForNewCards()
        {
            //Tries to add each card in cardsToBeAdded.
            foreach (UsefulCardData usefulData in cardsToBeAdded.ToList())
            {
                RejectReason? reason = AddCard(usefulData.card, usefulData.card.CardData, usefulData.customData);
                //If there is no reason it was rejected, that means it was accepted and it has been added. So it removes it from cardsToBeAdded
                if (reason == null)
                {
                    cardsToBeAdded.Remove(usefulData);
                }
            }
        }

        //This is what defines a "card" to me. With this data, i can do whatever I want with it.
        public struct UsefulCardData
        {
            public GameCard card;
            public CustomCardData customData;
            public UsefulCardData(GameCard card, CustomCardData customData)
            {
                this.card = card;
                this.customData = customData;
            }
        }

        //Reasons why a card wasn't accepted
        public enum RejectReason
        {
            AlreadyInGameobjects,
            IsBeingDragged,
            TooManyHoveringCards,
            IsUsefulInCurrentStack,
            NotResourceFoodOrBuilding,
            IsArm,
            IsGoingToCard,
            IsAlreadyInSomeArm
        }

        //This whole method takes a card, checks it for a bunch of things and returns a RejectReason if the card was rejected. Otherwise, it adds it to hoveringCards
        public RejectReason? AddCard(GameCard card, CardData cardData, CustomCardData customData)
        {
            if (card.BeingDragged == true) return RejectReason.IsBeingDragged;
            if (customData.arm != null) return RejectReason.IsAlreadyInSomeArm;
            foreach (GameObject gObject in gameObjects)
            {
                if (card.gameObject == gObject)
                {
                    return RejectReason.AlreadyInGameobjects;
                }
            }
            bool isUsefulInStack = false;
            GameCard rootCard = card.GetRootCard();
            if (rootCard != card && rootCard != null && IsCardBuilding(rootCard.CardData))
            {
                CardData rootCardData = rootCard.CardData;
                if (rootCardData != null)
                {
                    if (rootCardData.CanHaveCardOnTop(cardData))
                    {
                        isUsefulInStack = true;
                    }
                }
            }
            if (IsCardResource(cardData))
            {
                if (hoveringCards.Count < 8)
                {
                    if (customData.isGoingToCard == false)
                    {
                        if (!isUsefulInStack)
                        {
                            UsefulCardData usefulData = new UsefulCardData(card, customData);
                            hoveringCards.Add(usefulData);
                            gameObjects.Add(card.gameObject);
                            usefulData.card.PushEnabled = false;
                            usefulData.card.DragStartPosition = card.transform.position;
                            usefulData.card.RemoveFromStack();
                            usefulData.card.StartDragging();
                            customData.isHovering = true;
                            customData.arm = this;
                            separationAngle = pi * 2 / hoveringCards.Count;
                            return null;
                        }
                        else
                        {
                            return RejectReason.IsUsefulInCurrentStack;
                        }
                    }
                    else
                    {
                        return RejectReason.IsGoingToCard;
                    }
                }
                else
                {
                    return RejectReason.TooManyHoveringCards;
                }
            }
            else if (IsCardBuilding(cardData))
            {
                if (card.name.Substring(0, 3) != "Arm")
                {
                    UsefulCardData usefulBuildingData = new UsefulCardData(card, customData);
                    buildingsInRange.Add(usefulBuildingData);
                    return null;
                }
                else
                {
                    return RejectReason.IsArm;
                }
            }
            return RejectReason.NotResourceFoodOrBuilding;
        }

        //This whole method just removes a card from everything
        public void RemoveCard(GameCard card, CardData cardData, CustomCardData customData)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject == card.gameObject)
                {
                    gameObjects.Remove(card.gameObject);
                    break;
                }
            }
            if (IsCardBuilding(cardData))
            {
                foreach (UsefulCardData buildingInRange in buildingsInRange)
                {
                    if ((UnityEngine.Object)buildingInRange.card == (UnityEngine.Object)card)
                    {
                        buildingsInRange.Remove(buildingInRange);
                        break;
                    }
                }
            }
            else if (IsCardResource(cardData))
            {
                foreach (UsefulCardData hoveringCard in hoveringCards)
                {
                    if (hoveringCard.card == card)
                    {
                        hoveringCards.Remove(hoveringCard);
                        card.StopDragging();
                        customData.isHovering = false;
                        customData.arm = null;
                        separationAngle = pi * 2 / hoveringCards.Count;
                        break;
                    }
                }
            }
        }

        //This method is only used when the arm is being sold, so it just removes all of its cards. Pretty self explanatory to be honest.
        public void RemoveAllCards()
        {
            foreach (UsefulCardData usefulData in hoveringCards)
            {
                RemoveCard(usefulData.card, usefulData.card.CardData, usefulData.customData);
            }
            foreach (UsefulCardData usefulData in buildingsInRange)
            {
                RemoveCard(usefulData.card, usefulData.card.CardData, usefulData.customData);
            }
        }

        //Checks if a card is a building. poop and soil are buildings even though they aren't structures and isBuilding is false in their CardData but you can plant things on them so i want the arm to be able to send food to them
        public bool IsCardBuilding(CardData cardData)
        {
            return (cardData.MyCardType == CardType.Structures && cardData.IsBuilding == true) || cardData.Id == "poop" || cardData.Id == "soil";
        }

        //Pretty self explanatory. if a card is any one of these ids, it can grow food. So it's a food building
        public bool IsCardFoodBuilding(CardData cardData)
        {
            return (cardData.Id == "soil" || cardData.Id == "poop" || cardData.Id == "farm" || cardData.Id == "garden");
        }

        //Returns true if a card is a resource or food and not poop
        public bool IsCardResource(CardData cardData)
        {
            return (cardData.MyCardType == CardType.Resources || cardData.MyCardType == CardType.Food) && cardData.Id != "poop";
        }

        //Logic for if the arm should send a card to a building
        public bool ShouldSendCard(CardData buildingCardData, CardData hoveringCardData)
        {
            //If the building doesn't accept the card, don't send it
            if (!buildingCardData.CanHaveCardOnTop(hoveringCardData))
            {
                return false;
            }

            //If the building is already accepting a card, don't send it
            if (buildingCardData.MyGameCard.GetComponent<CustomCardData>().cardIncoming)
            {
                return false;
            }


            if (IsCardFoodBuilding(buildingCardData))
            {
                //If a card is a food building, send any food that isn't eggs or raw meat, because it can't be grown.
                if (hoveringCardData.MyCardType != CardType.Food || hoveringCardData.Id == "egg" || hoveringCardData.Id == "raw_meat")
                {
                    return false;
                }

                //Also if the food building isn't at the bottom of the stack, don't send it
                if (buildingCardData.MyGameCard.GetLeafCard() != buildingCardData.MyGameCard)
                {
                    return false;
                }
            }
            return true;
        }
    }

    //Just some data that every card holds
    public class CustomCardData : MonoBehaviour
    {
        public bool isHovering = false;
        public bool isGoingToCard = false;
        public bool cardIncoming = false;
        public Arm arm = null;
    }
}