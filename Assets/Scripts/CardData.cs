using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Card
{
    public string cardName;
    public CardType type;
    public Sprite cardSprite;
    public bool isExhausted = false;
    public int id; // Unique ID for market cards

    // Card effects based on opponent's card
    public Dictionary<CardType, CardEffect> effects;

    public Card(string name, CardType t, Sprite sprite, int cardId = 0)
    {
        cardName = name;
        type = t;
        cardSprite = sprite;
        id = cardId;
        effects = new Dictionary<CardType, CardEffect>();
    }
}

public enum CardType
{
    Green,
    Blue,
    Red,
    Yellow
}

[System.Serializable]
public class CardEffect
{
    public int gain = 0;
    public int steal = 0;
    public int bank = 0;
    public int opponentSteals = 0;
    public bool opponentGains = false;
    public bool buyRelic = false;
    public bool buyCard = false;
    public bool freeCard = false;
    public bool exhaustOpponent = false;
}

public class CardData : MonoBehaviour
{
    public static CardData Instance;

    [Header("Card Sprites")]
    public Sprite farmerSprite;
    public Sprite wallSprite;
    public Sprite raiderSprite;
    public Sprite merchantSprite;
    public Sprite cardBackSprite;

    [Header("Market Card Sprites")]
    public Sprite dungeonSprite;
    public Sprite treasurySprite;
    public Sprite picklerSprite;
    public Sprite ratCatcherSprite;
    public Sprite trapperSprite;

    public Dictionary<int, Card> allCards;

    void Awake()
    {
        Instance = this;
        InitializeCards();
    }

    void InitializeCards()
    {
        allCards = new Dictionary<int, Card>();

        // Basic Farmer (ID: 1)
        Card farmer = new Card("Farmer", CardType.Green, farmerSprite, 1);
        farmer.effects[CardType.Green] = new CardEffect { gain = 3 };
        farmer.effects[CardType.Blue] = new CardEffect { gain = 3 };
        farmer.effects[CardType.Red] = new CardEffect { gain = 3 };
        farmer.effects[CardType.Yellow] = new CardEffect { gain = 3 };
        allCards[1] = farmer;

        // Basic Wall (ID: 2)
        Card wall = new Card("Wall", CardType.Blue, wallSprite, 2);
        wall.effects[CardType.Green] = new CardEffect { gain = 1, bank = 1 };
        wall.effects[CardType.Blue] = new CardEffect { gain = 1, bank = 1 };
        wall.effects[CardType.Red] = new CardEffect { steal = 1, bank = 1 };
        wall.effects[CardType.Yellow] = new CardEffect { gain = 1, bank = 1 };
        allCards[2] = wall;

        // Basic Raider (ID: 3)
        Card raider = new Card("Raider", CardType.Red, raiderSprite, 3);
        raider.effects[CardType.Green] = new CardEffect { steal = 4 };
        raider.effects[CardType.Blue] = new CardEffect { }; // No effect
        raider.effects[CardType.Red] = new CardEffect { }; // No effect
        raider.effects[CardType.Yellow] = new CardEffect { steal = 4 };
        allCards[3] = raider;

        // Basic Merchant (ID: 4)
        Card merchant = new Card("Merchant", CardType.Yellow, merchantSprite, 4);
        merchant.effects[CardType.Green] = new CardEffect { buyRelic = true, buyCard = true };
        merchant.effects[CardType.Blue] = new CardEffect { buyRelic = true, buyCard = true };
        merchant.effects[CardType.Red] = new CardEffect { buyRelic = true, buyCard = true };
        merchant.effects[CardType.Yellow] = new CardEffect { buyRelic = true, buyCard = true };
        allCards[4] = merchant;

        // Market Cards
        // Dungeon (ID: 5) - Blue
        Card dungeon = new Card("Dungeon", CardType.Blue, dungeonSprite, 5);
        dungeon.effects[CardType.Green] = new CardEffect { gain = 1, bank = 2 };
        dungeon.effects[CardType.Blue] = new CardEffect { gain = 1, steal = 1, bank = 1 };
        dungeon.effects[CardType.Red] = new CardEffect { gain = 1, steal = 1, bank = 1 };
        dungeon.effects[CardType.Yellow] = new CardEffect { gain = 1, bank = 2 };
        allCards[5] = dungeon;

        // Treasury (ID: 6) - Blue
        Card treasury = new Card("Treasury", CardType.Blue, treasurySprite, 6);
        treasury.effects[CardType.Green] = new CardEffect { gain = 1, bank = 4 };
        treasury.effects[CardType.Blue] = new CardEffect { gain = 1, bank = 4 };
        treasury.effects[CardType.Red] = new CardEffect { steal = 2, bank = 4 };
        treasury.effects[CardType.Yellow] = new CardEffect { gain = 1, bank = 4 };
        allCards[6] = treasury;

        // Pickler (ID: 7) - Green
        Card pickler = new Card("Pickler", CardType.Green, picklerSprite, 7);
        pickler.effects[CardType.Green] = new CardEffect { gain = 4, bank = 2 };
        pickler.effects[CardType.Blue] = new CardEffect { gain = 4, bank = 2 };
        pickler.effects[CardType.Red] = new CardEffect { gain = 4 };
        pickler.effects[CardType.Yellow] = new CardEffect { gain = 4, bank = 2 };
        allCards[7] = pickler;

        // RatCatcher (ID: 8) - Green
        Card ratCatcher = new Card("RatCatcher", CardType.Green, ratCatcherSprite, 8);
        ratCatcher.effects[CardType.Green] = new CardEffect { gain = 6 };
        ratCatcher.effects[CardType.Blue] = new CardEffect { gain = 4 };
        ratCatcher.effects[CardType.Red] = new CardEffect { gain = 4 };
        ratCatcher.effects[CardType.Yellow] = new CardEffect { gain = 4 };
        allCards[8] = ratCatcher;

        // Trapper (ID: 9) - Red
        Card trapper = new Card("Trapper", CardType.Red, trapperSprite, 9);
        trapper.effects[CardType.Green] = new CardEffect { steal = 4, gain = 1 };
        trapper.effects[CardType.Blue] = new CardEffect { }; // No effect
        trapper.effects[CardType.Red] = new CardEffect { steal = 1 };
        trapper.effects[CardType.Yellow] = new CardEffect { steal = 4, gain = 1 };
        allCards[9] = trapper;
    }

    public Card GetCard(int id)
    {
        return allCards.ContainsKey(id) ? allCards[id] : null;
    }

    public Card CreateCardCopy(int id)
    {
        Card original = GetCard(id);
        if (original == null) return null;

        Card copy = new Card(original.cardName, original.type, original.cardSprite, original.id);
        copy.effects = new Dictionary<CardType, CardEffect>(original.effects);
        return copy;
    }
}
