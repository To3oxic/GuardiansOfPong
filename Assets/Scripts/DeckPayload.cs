using System.Collections.Generic;
using Unity.Netcode;

[System.Serializable]
public class DeckPayload : INetworkSerializable
{
    public List<string> cardNames = new List<string>();
    public string castingLegendName;
    public string legendStoneName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref castingLegendName);
        serializer.SerializeValue(ref legendStoneName);

        // Serialize list size
        int count = cardNames?.Count ?? 0;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
        {
            cardNames = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string cardName = string.Empty;
                serializer.SerializeValue(ref cardName);
                cardNames.Add(cardName);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                string cardName = cardNames[i];
                serializer.SerializeValue(ref cardName);
            }
        }
    }
}
