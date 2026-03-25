using UnityEngine;
using System.Collections.Generic;
using DropsAndLoadout;

public class RunDropsUI : MonoBehaviour
{
    private bool showRunDrops = false;

    void Update()
    {
        // Se a tecla Tab (ou outra) for pressionada, mostra o menu de drops da run atual
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            showRunDrops = !showRunDrops;
        }
    }

    void OnGUI()
    {
        if (showRunDrops && LoadoutSystem.Instance != null)
        {
            DrawRunDropsWindow();
        }
    }

    private void DrawRunDropsWindow()
    {
        int width = 300;
        int height = 400;
        int x = (Screen.width - width) / 2;
        int y = (Screen.height - height) / 2;

        GUI.Box(new Rect(x, y, width, height), "Armas Apanhadas Nesta Run");

        List<ItemDrop> drops = LoadoutSystem.Instance.RunDrops;

        if (drops.Count == 0)
        {
            GUI.Label(new Rect(x + 20, y + 50, width - 40, 30), "Nenhuma arma dropada ainda.");
            return;
        }

        // Variável temporária para guardar tooltip se o rato passar por cima
        string currentTooltip = "";

        // Simples view de texto
        int startY = y + 40;
        for (int i = 0; i < drops.Count; i++)
        {
            // Limitar a quantos podemos mostrar na janela
            if (i > 15)
            {
                GUI.Label(new Rect(x + 20, startY + (i * 20), width - 40, 20), "... e mais " + (drops.Count - 15) + "!");
                break;
            }

            WeaponRarity rarity = drops[i].Rarity;
            Rect labelRect = new Rect(x + 20, startY + (i * 20), width - 40, 20);

            GUI.color = GetColorForRarity(rarity);
            GUI.Label(labelRect, $"- {drops[i].ItemName} [{rarity}]");
            
            // Lógica de Tooltip
            if (labelRect.Contains(Event.current.mousePosition))
            {
                currentTooltip = drops[i].Description;
            }
            
            GUI.color = Color.white;
        }

        // Desenhar Tooltip no final para ficar por cima de tudo
        if (!string.IsNullOrEmpty(currentTooltip))
        {
            float mouseX = Event.current.mousePosition.x;
            float mouseY = Event.current.mousePosition.y;
            
            // Simples caixa preta com texto branco
            GUIStyle tooltipStyle = new GUIStyle(GUI.skin.box);
            tooltipStyle.wordWrap = true;
            tooltipStyle.normal.textColor = Color.white;
            
            float tooltipWidth = 200f;
            float tooltipHeight = tooltipStyle.CalcHeight(new GUIContent(currentTooltip), tooltipWidth) + 10f;
            
            // Draw Box and Text
            GUI.Box(new Rect(mouseX + 15, mouseY + 15, tooltipWidth, tooltipHeight), currentTooltip, tooltipStyle);
        }
    }

    private Color GetColorForRarity(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.SS: return Color.red;
            case WeaponRarity.S: return Color.yellow;
            case WeaponRarity.A: return Color.magenta;
            case WeaponRarity.B: return Color.cyan;
            case WeaponRarity.C: return Color.green;
            case WeaponRarity.D: default: return Color.white;
        }
    }
}
