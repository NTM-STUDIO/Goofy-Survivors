using UnityEngine;
using System.Collections.Generic;

namespace DropsAndLoadout
{
    public enum WeaponRarity
    {
        D,
        C,
        B,
        A,
        S,
        SS
    }

    public enum DropType
    {
        Weapon,
        Rune
    }

    public enum StatType
    {
        DanoFisico,
        DanoMagico,
        VidaMaxima,
        VelocidadeAtaque,
        ProbabilidadeCritico,
        DanoCritico,
        RouboDeVida,
        ReducaoRecarga
    }

    public enum WeaponArchetype
    {
        Melee,
        Ranged,
        Magic
    }

    [System.Serializable]
    public class ItemSubstat
    {
        public StatType Type;
        public float Value;

        public ItemSubstat(StatType type, float value)
        {
            Type = type;
            Value = value;
        }

        public string GetDescription()
        {
            return $"+{Value}% {Type}";
        }
    }

    [System.Serializable]
    public class ItemDrop
    {
        public string ItemUniqueId;
        public string ItemName; // Ou WeaponData
        public WeaponRarity Rarity;
        public DropType Type;
        public WeaponArchetype Archetype;
        public string Description; 
        public List<ItemSubstat> Substats;
        
        public ItemDrop(string name, WeaponRarity rarity, DropType type, WeaponArchetype archetype, string description = "")
        {
            ItemUniqueId = System.Guid.NewGuid().ToString();
            ItemName = name;
            Rarity = rarity;
            Type = type;
            Archetype = archetype;
            Description = description;
            Substats = new List<ItemSubstat>();
            RollInitialSubstats();
        }

        public void RollInitialSubstats()
        {
            Substats.Clear();
            int numSubstats = Rarity switch
            {
                WeaponRarity.D => 1,
                WeaponRarity.C => 2,
                WeaponRarity.B => 3,
                WeaponRarity.A => 4,
                WeaponRarity.S => 5,
                WeaponRarity.SS => 6,
                _ => 1
            };

            for (int i = 0; i < numSubstats; i++)
            {
                var statTypes = (StatType[])System.Enum.GetValues(typeof(StatType));
                StatType randomStat = statTypes[UnityEngine.Random.Range(0, statTypes.Length)];
                float randomValue = UnityEngine.Random.Range(1f, 10f); // Example value range
                randomValue = (float)System.Math.Round(randomValue, 1);
                Substats.Add(new ItemSubstat(randomStat, randomValue));
            }
        }

        public void ApplyRerollItem()
        {
            RollInitialSubstats();
        }
    }

    public class WeaponDropSystem : MonoBehaviour
    {
        public static WeaponDropSystem Instance { get; private set; }

        [Header("Drop Settings")]
        [Tooltip("Chance of dropping a weapon when an enemy dies (0.0001 = 0.01%)")]
        [Range(0f, 1f)]
        public float baseWeaponDropChance = 0.0001f; 

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public ItemDrop PreRollDrop()
        {
            float randomRoll = Random.value;
            // 0.0001 = 0.01%
            if (randomRoll <= baseWeaponDropChance)
            {
                WeaponRarity rolledRarity = DetermineRarity();
                DropType dropType = Random.value < 0.5f ? DropType.Weapon : DropType.Rune;
                
                string desc = dropType == DropType.Weapon
                    ? $"Uma misteriosa arma de raridade {rolledRarity}. Aumenta exponencialmente o teu dano."
                    : $"Uma runa anciã de raridade {rolledRarity}. Concede-te poderes e proteção mística.";
                
                string itemName = dropType == DropType.Weapon ? "Mysterious Random Weapon" : "Ancient Rune";
                
                WeaponArchetype randomArchetype = (WeaponArchetype)UnityEngine.Random.Range(0, 3);
                return new ItemDrop(itemName, rolledRarity, dropType, randomArchetype, desc);
            }
            return null;
        }

        public void SpawnPreRolledItemDrop(ItemDrop preRolledItem, Vector3 position)
        {
            if (preRolledItem == null) return;

            Debug.Log($"[WeaponDropSystem] Dropped a {preRolledItem.Rarity} {preRolledItem.Type} at {position} (Pre-rolled)!");
            if (LoadoutSystem.Instance != null)
            {
                LoadoutSystem.Instance.AddRunDrop(preRolledItem);
            }
        }

        public void RollForWeaponDrop(Vector3 dropPosition)
        {
            float randomRoll = Random.value;
            
            // 0.0001 = 0.01%
            if (randomRoll <= baseWeaponDropChance)
            {
                WeaponRarity rolledRarity = DetermineRarity();
                DropType dropType = Random.value < 0.5f ? DropType.Weapon : DropType.Rune;
                SpawnItemDrop(dropPosition, rolledRarity, dropType);
            }
        }

        private WeaponRarity DetermineRarity()
        {
            float roll = Random.Range(0f, 100f);

            // SS: 0.1%
            // S: 1%
            // A: 5%
            // B: 20%
            // C: 30%
            // D: 43.9%
            
            if (roll <= 0.1f) return WeaponRarity.SS;
            if (roll <= 1.1f) return WeaponRarity.S;    // 0.1 + 1.0
            if (roll <= 6.1f) return WeaponRarity.A;    // 1.1 + 5.0
            if (roll <= 26.1f) return WeaponRarity.B;   // 6.1 + 20.0
            if (roll <= 56.1f) return WeaponRarity.C;   // 26.1 + 30.0
            
            return WeaponRarity.D;
        }

        private void SpawnItemDrop(Vector3 position, WeaponRarity rarity, DropType type)
        {
            Debug.Log($"[WeaponDropSystem] Dropped a {rarity} {type} at {position}!");
            // In the future this should instantiate a physical drop orb/chest for the player to pick up
            // Or add directly to the loadout inventory
            
            // Temporary Direct add for demonstration:
            if (LoadoutSystem.Instance != null)
            {
                string desc = type == DropType.Weapon 
                    ? $"Uma misteriosa arma de raridade {rarity}. Aumenta exponencialmente o teu dano." 
                    : $"Uma runa anciã de raridade {rarity}. Concede-te poderes e proteção mística.";
                    
                string itemName = type == DropType.Weapon ? "Mysterious Random Weapon" : "Ancient Rune";
                WeaponArchetype randomArchetype = (WeaponArchetype)UnityEngine.Random.Range(0, 3);
                // Agora o drop vai para a RUN atual, e não direto para o inventário final
                LoadoutSystem.Instance.AddRunDrop(new ItemDrop(itemName, rarity, type, randomArchetype, desc));
            }
        }
    }
}