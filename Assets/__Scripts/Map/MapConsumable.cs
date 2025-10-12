using UnityEngine;

// Componente a colocar no arbusto (ou outro "consumível" do mapa)

// Objetivo: quando o Player entra em contacto (Trigger ou Colisão) com este objeto,
// é instanciado um item (prefab) escolhido por pesos a partir da lista de drops.

// Requisitos de física 2D para os eventos dispararem:
// - Este objeto: precisa de um Collider2D (obrigatório por [RequireComponent]).
// - Player: precisa de um Rigidbody2D OU este objeto pode ter um Rigidbody2D.
//   Pelo menos um dos dois participantes do contacto deve ter Rigidbody2D.
// Notas:
// - Se usar Trigger (recomendado), marque dropOnTrigger e o collider será isTrigger.
// - Se usar Colisão, marque dropOnCollision e o collider NÃO deve ser isTrigger.
// - onlyOnce controla se faz o drop só na primeira interação.
// - destroyAfterDrop permite remover o arbusto após o drop, com atraso opcional.

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MapConsumable : MonoBehaviour
{
    [System.Serializable]
    public class DropEntry
    {
        // Prefab do item a instanciar quando o drop for escolhido
        public GameObject prefab;
        // Peso relativo da probabilidade do item sair (quanto maior, mais provável)
        public float weight = 1f;
        // Offset em relação à posição do arbusto onde o item será instanciado
        [Tooltip("Offset relativo ao arbusto onde o item aparece")] public Vector2 spawnOffset;
    }

    [Header("Tabela de Drops (por pesos)")]
    // Lista configurável no Inspetor. Um destes itens será escolhido por pesos.
    public DropEntry[] drops;

    [Header("Deteção do Player")]
    // Tag que identifica o Player (o objeto que ativa o drop)
    [Tooltip("Tag usada pelo Player")] public string playerTag = "Player";
    // Ative para usar eventos de Trigger 2D (OnTriggerEnter2D/Stay). Exige collider.isTrigger = true
    [Tooltip("Se verdadeiro, usa OnTriggerEnter2D (collider isTrigger)")] public bool dropOnTrigger = true;
    // Ative para usar eventos de Colisão 2D (OnCollisionEnter2D/Stay). Exige collider.isTrigger = false
    [Tooltip("Se verdadeiro, usa OnCollisionEnter2D (collider normal)")] public bool dropOnCollision = false;

    [Header("Comportamento após Drop")]
    // Se true, só dropa na primeira interação e ignora próximas
    [Tooltip("Se verdadeiro, só dropa uma vez")] public bool onlyOnce = true;
    // Se true, destrói este objeto depois de dropar
    [Tooltip("Se verdadeiro, destrói este objeto após dropar")] public bool destroyAfterDrop = false;
    // Atraso em segundos para destruir (se ativado)
    public float destroyDelay = 0f;

    [Header("Debug")]
    // Mostra mensagens úteis no Console para diagnosticar (tags, eventos, item dropado)
    public bool debugLogs = false;

    // Guarda estado para não dropar outra vez quando onlyOnce está ativo
    private bool hasDropped = false;

    void Awake()
    {
        // Mantém o collider coerente com as flags dropOnTrigger/dropOnCollision
        SyncColliderMode();
#if UNITY_EDITOR
        // Ajuda no Editor: alerta se o Player não tiver Rigidbody2D (necessário para eventos 2D)
        var player = GameObject.FindWithTag(playerTag);
        if (player != null && player.GetComponent<Rigidbody2D>() == null)
        {
            Debug.LogWarning($"MapConsumable em '{name}': O objeto com tag '{playerTag}' não tem Rigidbody2D. Eventos de Trigger/Collision 2D requerem pelo menos um Rigidbody2D.");
        }
#endif
    }

    void Reset()
    {
        // Chamado quando o componente é adicionado ou resetado no Inspetor
        SyncColliderMode();
    }

    void OnValidate()
    {
        // Garante configuração consistente ao editar valores no Inspetor
        SyncColliderMode();
    }

    // Define isTrigger do Collider2D conforme as flags escolhidas
    private void SyncColliderMode()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        // Se ambos estiverem ativos, prioriza Trigger para evitar confusões
        if (dropOnTrigger && !dropOnCollision)
            col.isTrigger = true;
        else if (dropOnCollision && !dropOnTrigger)
            col.isTrigger = false;
        else if (dropOnTrigger && dropOnCollision)
            col.isTrigger = true;
        else
        {
            // Nenhum modo ativo -> não haverá eventos, log opcional de aviso
            if (debugLogs) Debug.LogWarning($"MapConsumable em '{name}': dropOnTrigger e dropOnCollision estão desativados. Nenhum drop irá ocorrer.");
        }
    }

    // Disparado quando outro collider entra no trigger deste objeto
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!dropOnTrigger) return; // Modo Trigger desativado
        if (!other.CompareTag(playerTag))
        {
            if (debugLogs) Debug.Log($"MapConsumable '{name}': Trigger com '{other.name}' ignorado (tag {other.tag}).");
            return;
        }
        TryDrop();
    }

    // Disparado a cada frame enquanto outro collider permanece sobreposto ao trigger
    private void OnTriggerStay2D(Collider2D other)
    {
        // Fallback útil caso o jogador já esteja sobreposto no início da cena
        if (!dropOnTrigger) return;
        if (onlyOnce && hasDropped) return;
        if (!other.CompareTag(playerTag)) return;
        TryDrop();
    }

    // Disparado quando ocorre uma colisão física (isTrigger = false)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!dropOnCollision) return; // Modo Colisão desativado
        if (!collision.collider.CompareTag(playerTag))
        {
            if (debugLogs) Debug.Log($"MapConsumable '{name}': Collision com '{collision.collider.name}' ignorado (tag {collision.collider.tag}).");
            return;
        }
        TryDrop();
    }

    // Disparado a cada frame enquanto a colisão persiste
    private void OnCollisionStay2D(Collision2D collision)
    {
        // Fallback caso o objeto já comece encostado
        if (!dropOnCollision) return;
        if (onlyOnce && hasDropped) return;
        if (!collision.collider.CompareTag(playerTag)) return;
        TryDrop();
    }

    // Fluxo central para executar o drop
    private void TryDrop()
    {
        // Respeita o modo single-drop
        if (onlyOnce && hasDropped) return;
        // Sem tabela de drops não há o que fazer
        if (drops == null || drops.Length == 0) return;

        var chosen = GetWeightedRandomDrop();
        if (chosen != null && chosen.prefab != null)
        {
            // Instancia exatamente UM item na posição do arbusto + offset
            Vector3 spawnPos = transform.position + (Vector3)chosen.spawnOffset;
            Instantiate(chosen.prefab, spawnPos, Quaternion.identity);
            if (debugLogs) Debug.Log($"MapConsumable '{name}': Dropou '{chosen.prefab.name}' em {spawnPos}.");
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"MapConsumable '{name}': Nenhum drop válido encontrado.");
        }

        // Marca que já dropou (se onlyOnce estiver ativo, impedirá futuras execuções)
        hasDropped = true;

        // Destrói o arbusto após o drop, se configurado
        if (destroyAfterDrop)
            Destroy(gameObject, destroyDelay);
    }

    // Escolhe uma entrada de drop com base em pesos (roleta viciada)
    private DropEntry GetWeightedRandomDrop()
    {
        // Soma dos pesos válidos (> 0)
        float totalWeight = 0f;
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d != null && d.weight > 0f)
                totalWeight += d.weight;
        }
        if (totalWeight <= 0f) return null; // Todos os pesos são 0 ou a lista está inválida

        // Escolhe um número no intervalo [0, totalWeight] e percorre acumulando
        float r = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d == null || d.weight <= 0f) continue;
            cumulative += d.weight;
            // Quando o acumulado ultrapassa o número sorteado, encontramos o drop
            if (r <= cumulative)
                return d;
        }
        // Fallback defensivo (não deve ocorrer, mas evita null)
        return drops[drops.Length - 1];
    }
}
