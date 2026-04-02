using System.Collections.Generic;
using UnityEngine;
using StaticDrift.Cards;
using StaticDrift.Synergy;

namespace StaticDrift.Managers
{
    public class DeckManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SynergyManager _synergyManager;

        [Header("Deck Slots")]
        [SerializeField] private int _maxSlots = 8;

        [SerializeField] private List<CardData> _startingDeck = new List<CardData>();

        private readonly List<CardData> _activeDeck = new List<CardData>();
        private int _usedSlots;

        public int MaxSlots => _maxSlots;
        public int UsedSlots => _usedSlots;
        public IReadOnlyList<CardData> ActiveDeck => _activeDeck;

        private void Awake()
        {
            InitializeDeck();
        }

        private void InitializeDeck()
        {
            _activeDeck.Clear();
            _usedSlots = 0;

            int count = _startingDeck != null ? _startingDeck.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CardData card = _startingDeck[i];
                if (card == null)
                {
                    continue;
                }

                TryAddCard(card);
            }

            RebuildSynergy();
        }

        public bool TryAddCard(CardData card)
        {
            if (card == null)
            {
                return false;
            }

            int slotsRequired = card.IsHeavy ? 2 : 1;
            if (_usedSlots + slotsRequired > _maxSlots)
            {
                return false;
            }

            _activeDeck.Add(card);
            _usedSlots += slotsRequired;

            RebuildSynergy();
            return true;
        }

        public bool RemoveCard(CardData card)
        {
            if (card == null)
            {
                return false;
            }

            int index = IndexOfCard(card);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public bool PurgeCardAt(int index)
        {
            if (index < 0 || index >= _activeDeck.Count)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        private int IndexOfCard(CardData card)
        {
            int count = _activeDeck.Count;
            for (int i = 0; i < count; i++)
            {
                if (_activeDeck[i] == card)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveAt(int index)
        {
            CardData card = _activeDeck[index];
            int slotsFreed = card != null && card.IsHeavy ? 2 : 1;

            _activeDeck.RemoveAt(index);
            _usedSlots -= slotsFreed;
            if (_usedSlots < 0)
            {
                _usedSlots = 0;
            }

            RebuildSynergy();
        }

        private void RebuildSynergy()
        {
            if (_synergyManager == null)
            {
                return;
            }

            _synergyManager.RebuildFromDeck(_activeDeck);
        }
    }
}
