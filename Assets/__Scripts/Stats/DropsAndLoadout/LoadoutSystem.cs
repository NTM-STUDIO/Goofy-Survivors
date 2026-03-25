using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace DropsAndLoadout
{
    [System.Serializable]
    public class InventorySaveData
    {
        public List<ItemDrop> Inventory = new List<ItemDrop>();
        public int RerollTokens = 0;
    }

    [DefaultExecutionOrder(-110)]
    public class LoadoutSystem : MonoBehaviour
    {
        public static LoadoutSystem Instance { get; private set; }

        [Header("Loadout State")]
        public List<ItemDrop> Inventory = new List<ItemDrop>();
        public ItemDrop[] EquippedWeapons = new ItemDrop[6]; // Typical Max 6 weapons for Survivor games
        public List<string> EquippedRunes = new List<string>();
        public int RerollTokens = 0;

        [Header("Session State")]
        public List<ItemDrop> RunDrops = new List<ItemDrop>(); // Armas dropadas nesta run atual

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadInventory();
        }

        private string SavePath => Path.Combine(Application.persistentDataPath, "inventory.json");

        public void SaveInventory()
        {
            InventorySaveData data = new InventorySaveData 
            { 
                Inventory = this.Inventory,
                RerollTokens = this.RerollTokens 
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[LoadoutSystem] Inventário salvo em {SavePath}");
        }

        public void LoadInventory()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(json);
                if (data != null && data.Inventory != null)
                {
                    this.Inventory = data.Inventory;
                    this.RerollTokens = data.RerollTokens;
                    Debug.Log($"[LoadoutSystem] Inventário carregado de {SavePath}. Total: {Inventory.Count}");
                }
            }
            
            if (!File.Exists(SavePath) || Inventory.Count == 0)
            {
                Inventory.Add(new ItemDrop("FlyingPan", WeaponRarity.D, DropType.Weapon, WeaponArchetype.Melee, "A trusty pan"));
                SaveInventory();
                Debug.Log("[LoadoutSystem] Inventário vazio/novo. Adicionada 'FlyingPan' por defeito.");
            }
        }

        public void AddWeaponToInventory(ItemDrop newWeapon)
        {
            Inventory.Add(newWeapon);
            SaveInventory();
            Debug.Log($"[LoadoutSystem] Adicionada {newWeapon.ItemName} ({newWeapon.Rarity}) ao inventário. Total de itens: {Inventory.Count}");
        }

        public void AddRunDrop(ItemDrop newDrop)
        {
            RunDrops.Add(newDrop);
            Debug.Log($"[LoadoutSystem] Drop apanhado na Run: {newDrop.ItemName} ({newDrop.Rarity})");
        }

        public void CommitRunDrops()
        {
            Inventory.AddRange(RunDrops);
            RunDrops.Clear();
            SaveInventory();
            Debug.Log($"[LoadoutSystem] Vitória! Drops da run transferidos para o inventário principal.");
        }

        public void ClearRunDrops()
        {
            RunDrops.Clear();
            Debug.Log($"[LoadoutSystem] Derrota! Drops da run foram perdidos.");
        }

        public bool EquipWeapon(int inventoryIndex, int slotIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= Inventory.Count) return false;
            if (slotIndex < 0 || slotIndex >= EquippedWeapons.Length) return false;

            // Swap Logic
            ItemDrop temp = EquippedWeapons[slotIndex];
            EquippedWeapons[slotIndex] = Inventory[inventoryIndex];
            
            if (temp != null)
            {
                // Se já houver algo lá, guarda na lista no mesmo índice
                Inventory[inventoryIndex] = temp;
            }
            else
            {
                // Liberta do inventário
                Inventory.RemoveAt(inventoryIndex);
            }

            Debug.Log($"[LoadoutSystem] Arma equipada no slot {slotIndex}.");
            return true;
        }

        public void UnequipWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= EquippedWeapons.Length) return;

            if (EquippedWeapons[slotIndex] != null)
            {
                Inventory.Add(EquippedWeapons[slotIndex]);
                EquippedWeapons[slotIndex] = null;
            }
        }
        
        public void AddRune(string runeId)
        {
            if(!EquippedRunes.Contains(runeId))
            {
                EquippedRunes.Add(runeId);
                Debug.Log($"[LoadoutSystem] Runa Equipada: {runeId}");
            }
        }
    }
}
