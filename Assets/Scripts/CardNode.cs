using UnityEngine;

[System.Serializable]
public class CardNode
{
    public CardData cardData;
    public CardNode next;
    public CardNode previous;

    public CardNode(CardData data)
    {
        cardData = data;
        next = null;
        previous = null;
    }
}

public class CircularCardDeck
{
    private CardNode head;
    private int count;

    public int Count => count;

    public void AddCard(CardData cardData)
    {
        CardNode newNode = new CardNode(ScriptableObject.Instantiate(cardData));

        if (head == null)
        {
            // First card - point to itself
            head = newNode;
            newNode.next = newNode;
            newNode.previous = newNode;
        }
        else
        {
            // Insert at the end (before head)
            CardNode tail = head.previous;

            newNode.next = head;
            newNode.previous = tail;
            tail.next = newNode;
            head.previous = newNode;
        }
        count++;
    }

    public CardNode RemoveCard(CardData cardToRemove)
    {
        if (head == null || count == 0) return null;

        CardNode current = head;
        do
        {
            if (current.cardData == cardToRemove)
            {
                // Found the card to remove
                if (count == 1)
                {
                    // Only one card left
                    head = null;
                }
                else
                {
                    // Reconnect the links
                    current.previous.next = current.next;
                    current.next.previous = current.previous;

                    // Update head if we're removing the head
                    if (current == head)
                    {
                        head = current.next;
                    }
                }

                count--;
                return current;
            }
            current = current.next;
        } while (current != head);

        return null; // Card not found
    }

    public void AddCardToEnd(CardData cardData)
    {
        AddCard(cardData); // Same as AddCard since we always add to the end
    }

    public CardNode RemoveAndCycleCard(CardData cardToRemove)
    {
        CardNode removedNode = RemoveCard(cardToRemove);
        if (removedNode != null)
        {
            // Add a fresh copy of the card to the end
            AddCardToEnd(removedNode.cardData);
        }
        return removedNode;
    }

    public CardNode GetCardAt(int index)
    {
        if (head == null || index < 0 || index >= count) return null;

        CardNode current = head;
        for (int i = 0; i < index; i++)
        {
            current = current.next;
        }
        return current;
    }

    public CardData[] GetVisibleCards(int maxCards)
    {
        if (head == null) return new CardData[0];

        CardData[] visibleCards = new CardData[Mathf.Min(maxCards, count)];
        CardNode current = head;

        for (int i = 0; i < visibleCards.Length; i++)
        {
            visibleCards[i] = current.cardData;
            current = current.next;
        }

        return visibleCards;
    }

    public void Clear()
    {
        head = null;
        count = 0;
    }
}