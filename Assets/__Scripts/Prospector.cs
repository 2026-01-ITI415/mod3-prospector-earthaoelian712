using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Deck))]
[RequireComponent(typeof(JsonParseLayout))]
public class Prospector : MonoBehaviour
{
    private static Prospector S;

    [Header("Dynamic")]
    public List<CardProspector> drawPile = new List<CardProspector>();
    public List<CardProspector> discardPile = new List<CardProspector>();
    public List<CardProspector> mine = new List<CardProspector>();
    public CardProspector target;

    private Transform layoutAnchor;

    private Deck deck;
    private JsonLayout jsonLayout;

    // A Dictionary to pair mine layout IDs and actual Cards
    private Dictionary<int, CardProspector> mineIdToCardDict;

    void Start()
    {
        // Set the private Singleton
        if (S != null) Debug.LogError("Attempted to set S more than once!");
        S = this;

        jsonLayout = GetComponent<JsonParseLayout>().layout;

        deck = GetComponent<Deck>();

        // Initialize and shuffle deck
        deck.InitDeck();
        Deck.Shuffle(ref deck.cards);

        drawPile = ConvertCardsToCardProspectors(deck.cards);

        // Lay out the mine first
        LayoutMine();

        // IMPORTANT FIX:
        // Refresh which mine cards should actually be face-up at game start
        SetMineFaceUps();

        // Draw initial target and arrange draw pile
        MoveToTarget(Draw());
        UpdateDrawPile();
    }

    /// <summary>
    /// Converts each Card in a List<Card> into a List<CardProspector>.
    /// </summary>
    List<CardProspector> ConvertCardsToCardProspectors(List<Card> listCard)
    {
        List<CardProspector> listCP = new List<CardProspector>();
        CardProspector cp;

        foreach (Card card in listCard)
        {
            cp = card as CardProspector;
            listCP.Add(cp);
        }

        return listCP;
    }

    /// <summary>
    /// Pulls a single card from the beginning of the drawPile and returns it.
    /// </summary>
    CardProspector Draw()
    {
        CardProspector cp = drawPile[0];
        drawPile.RemoveAt(0);
        return cp;
    }

    /// <summary>
    /// Positions the initial tableau of cards, a.k.a. the "mine".
    /// </summary>
    void LayoutMine()
    {
        // Create an empty GameObject to serve as an anchor for the tableau
        if (layoutAnchor == null)
        {
            GameObject tGO = new GameObject("_LayoutAnchor");
            layoutAnchor = tGO.transform;
        }

        CardProspector cp;

        // Generate the Dictionary to match mine layout ID to CardProspector
        mineIdToCardDict = new Dictionary<int, CardProspector>();

        // Iterate through the JsonLayoutSlots pulled from the JSON_Layout
        foreach (JsonLayoutSlot slot in jsonLayout.slots)
        {
            cp = Draw();

            // Set initial face value from JSON; SetMineFaceUps() will correct it after layout
            cp.faceUp = slot.faceUp;

            // Make the CardProspector a child of layoutAnchor
            cp.transform.SetParent(layoutAnchor);

            // Convert the last char of the layer string to an int (e.g. "Row0")
            int z = int.Parse(slot.layer[slot.layer.Length - 1].ToString());

            // Set the localPosition of the card based on the slot information
            cp.SetLocalPos(new Vector3(
                jsonLayout.multiplier.x * slot.x,
                jsonLayout.multiplier.y * slot.y,
                -z
            ));

            cp.layoutID = slot.id;
            cp.layoutSlot = slot;

            // CardProspectors in the mine have the state mine
            cp.state = eCardState.mine;

            // Set the sorting layer of all SpriteRenderers on the Card
            cp.SetSpriteSortingLayer(slot.layer);

            mine.Add(cp);
            mineIdToCardDict.Add(slot.id, cp);
        }
    }

    /// <summary>
    /// Moves the current target card to the discardPile.
    /// </summary>
    void MoveToDiscard(CardProspector cp)
    {
        cp.state = eCardState.discard;
        discardPile.Add(cp);
        cp.transform.SetParent(layoutAnchor);

        cp.SetLocalPos(new Vector3(
            jsonLayout.multiplier.x * jsonLayout.discardPile.x,
            jsonLayout.multiplier.y * jsonLayout.discardPile.y,
            0
        ));

        cp.faceUp = true;

        cp.SetSpriteSortingLayer(jsonLayout.discardPile.layer);
        cp.SetSortingOrder(-200 + (discardPile.Count * 3));
    }

    /// <summary>
    /// Make cp the new target card.
    /// </summary>
    void MoveToTarget(CardProspector cp)
    {
        if (target != null) MoveToDiscard(target);

        MoveToDiscard(cp);

        target = cp;
        cp.state = eCardState.target;

        cp.SetSpriteSortingLayer("Target");
        cp.SetSortingOrder(0);
    }

    /// <summary>
    /// Arranges all the cards of the drawPile to show how many are left.
    /// </summary>
    void UpdateDrawPile()
    {
        CardProspector cp;

        for (int i = 0; i < drawPile.Count; i++)
        {
            cp = drawPile[i];
            cp.transform.SetParent(layoutAnchor);

            Vector3 cpPos = new Vector3();
            cpPos.x = jsonLayout.multiplier.x * jsonLayout.drawPile.x;
            cpPos.x += jsonLayout.drawPile.xStagger * i;
            cpPos.y = jsonLayout.multiplier.y * jsonLayout.drawPile.y;
            cpPos.z = 0.1f * i;

            cp.SetLocalPos(cpPos);

            cp.faceUp = false;
            cp.state = eCardState.drawpile;

            cp.SetSpriteSortingLayer(jsonLayout.drawPile.layer);
            cp.SetSortingOrder(-10 * i);
        }
    }

    /// <summary>
    /// This turns cards in the Mine face-up and face-down.
    /// </summary>
    public void SetMineFaceUps()
    {
        CardProspector coverCP;

        foreach (CardProspector cp in mine)
        {
            bool faceUp = true;

            // Iterate through the covering cards by mine layout ID
            foreach (int coverID in cp.layoutSlot.hiddenBy)
            {
                coverCP = mineIdToCardDict[coverID];

                // If the covering card is null or still in the mine,
                // this card should stay face-down
                if (coverCP == null || coverCP.state == eCardState.mine)
                {
                    faceUp = false;
                }
            }

            cp.faceUp = faceUp;
        }
    }

    /// <summary>
    /// Test whether the game is over.
    /// </summary>
    void CheckForGameOver()
    {
        if (mine.Count == 0)
        {
            GameOver(true);
            return;
        }

        if (drawPile.Count > 0) return;

        foreach (CardProspector cp in mine)
        {
            if (target.AdjacentTo(cp)) return;
        }

        GameOver(false);
    }

    /// <summary>
    /// Called when the game is over.
    /// </summary>
    void GameOver(bool won)
    {
        if (won)
        {
            ScoreManager.TALLY(eScoreEvent.gameWin);
        }
        else
        {
            ScoreManager.TALLY(eScoreEvent.gameLoss);
        }

        CardSpritesSO.RESET();
        SceneManager.LoadScene("__Prospector_Scene_0");
    }

    /// <summary>
    /// Handler for any time a card in the game is clicked.
    /// </summary>
    static public void CARD_CLICKED(CardProspector cp)
    {
        switch (cp.state)
        {
            case eCardState.target:
                // Clicking the target card does nothing
                break;

            case eCardState.drawpile:
                // Clicking any card in the drawPile draws the next card
                S.MoveToTarget(S.Draw());
                S.UpdateDrawPile();
                ScoreManager.TALLY(eScoreEvent.draw);
                break;

            case eCardState.mine:
                bool validMatch = true;

                // If the card is face-down, it’s not valid
                if (!cp.faceUp) validMatch = false;

                // If it’s not an adjacent rank, it’s not valid
                if (!cp.AdjacentTo(S.target)) validMatch = false;

                if (validMatch)
                {
                    S.mine.Remove(cp);
                    S.MoveToTarget(cp);

                    // Refresh mine after removing a card
                    S.SetMineFaceUps();
                    ScoreManager.TALLY(eScoreEvent.mine);
                }
                break;
        }

        S.CheckForGameOver();
    }
}