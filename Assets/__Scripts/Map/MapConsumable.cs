using UnityEngine;

// Componente a colocar no arbusto (ou outro "consum�vel" do mapa)

// Objetivo: quando o Player entra em contacto (Trigger ou Colis�o) com este objeto,
// � instanciado um item (prefab) escolhido por pesos a partir da lista de drops.

// Requisitos de f�sica 2D para os eventos dispararem:
// - Este objeto: precisa de um Collider2D (obrigat�rio por [RequireComponent]).
// - Player: precisa de um Rigidbody2D OU este objeto pode ter um Rigidbody2D.
//   Pelo menos um dos dois participantes do contacto deve ter Rigidbody2D.
// Notas:
// - Se usar Trigger (recomendado), marque dropOnTrigger e o collider ser� isTrigger.
// - Se usar Colis�o, marque dropOnCollision e o collider N�O deve ser isTrigger.
// - onlyOnce controla se faz o drop s� na primeira intera��o.
// - destroyAfterDrop permite remover o arbusto ap�s o drop, com atraso opcional.

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MapConsumable : MonoBehaviour
{
    [System.Serializable]
    public class DropEntry
    {
        // Prefab do item a instanciar quando o drop for escolhido
        public GameObject prefab;
        // Peso relativo da probabilidade do item sair (quanto maior, mais prov�vel)
        public float weight = 1f;
        // Offset em rela��o � posi��o do arbusto onde o item ser� instanciado
        [Tooltip("Offset relativo ao arbusto onde o item aparece")] public Vector2 spawnOffset;
    }

    [Header("Tabela de Drops (por pesos)")]
    // Lista configur�vel no Inspetor. Um destes itens ser� escolhido por pesos.
    public DropEntry[] drops;

    [Header("Dete��o do Player")]
    // Tag que identifica o Player (o objeto que ativa o drop)
    [Tooltip("Tag usada pelo Player")] public string playerTag = "Player";
    // Ative para usar eventos de Trigger 2D (OnTriggerEnter2D/Stay). Exige collider.isTrigger = true
    [Tooltip("Se verdadeiro, usa OnTriggerEnter2D (collider isTrigger)")] public bool dropOnTrigger = true;
    // Ative para usar eventos de Colis�o 2D (OnCollisionEnter2D/Stay). Exige collider.isTrigger = false
    [Tooltip("Se verdadeiro, usa OnCollisionEnter2D (collider normal)")] public bool dropOnCollision = false;

    [Header("Comportamento ap�s Drop")]
    // Se true, s� dropa na primeira intera��o e ignora pr�ximas
    [Tooltip("Se verdadeiro, s� dropa uma vez")] public bool onlyOnce = true;
    // Se true, destr�i este objeto depois de dropar
    [Tooltip("Se verdadeiro, destr�i este objeto ap�s dropar")] public bool destroyAfterDrop = false;
    // Atraso em segundos para destruir (se ativado)
    public float destroyDelay = 0f;

    [Header("Debug")]
    // Mostra mensagens �teis no Console para diagnosticar (tags, eventos, item dropado)
    public bool debugLogs = false;

    // Guarda estado para n�o dropar outra vez quando onlyOnce est� ativo
    private bool hasDropped = false;

    void Awake()
    {
        // Mant�m o collider coerente com as flags dropOnTrigger/dropOnCollision
        SyncColliderMode();
#if UNITY_EDITOR
        // Ajuda no Editor: alerta se o Player n�o tiver Rigidbody2D (necess�rio para eventos 2D)
        var player = GameObject.FindWithTag(playerTag);
        if (player != null && player.GetComponent<Rigidbody2D>() == null)
        {
            Debug.LogWarning($"MapConsumable em '{name}': O objeto com tag '{playerTag}' n�o tem Rigidbody2D. Eventos de Trigger/Collision 2D requerem pelo menos um Rigidbody2D.");
        }
#endif
    }

    void Reset()
    {
        // Chamado quando o componente � adicionado ou resetado no Inspetor
        SyncColliderMode();
    }

    void OnValidate()
    {
        // Garante configura��o consistente ao editar valores no Inspetor
        SyncColliderMode();
    }

    // Define isTrigger do Collider2D conforme as flags escolhidas
    private void SyncColliderMode()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        // Se ambos estiverem ativos, prioriza Trigger para evitar confus�es
        if (dropOnTrigger && !dropOnCollision)
            col.isTrigger = true;
        else if (dropOnCollision && !dropOnTrigger)
            col.isTrigger = false;
        else if (dropOnTrigger && dropOnCollision)
            col.isTrigger = true;
        else
        {
            // Nenhum modo ativo -> n�o haver� eventos, log opcional de aviso
            if (debugLogs) Debug.LogWarning($"MapConsumable em '{name}': dropOnTrigger e dropOnCollision est�o desativados. Nenhum drop ir� ocorrer.");
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
        // Fallback �til caso o jogador j� esteja sobreposto no in�cio da cena
        if (!dropOnTrigger) return;
        if (onlyOnce && hasDropped) return;
        if (!other.CompareTag(playerTag)) return;
        TryDrop();
    }

    // Disparado quando ocorre uma colis�o f�sica (isTrigger = false)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!dropOnCollision) return; // Modo Colis�o desativado
        if (!collision.collider.CompareTag(playerTag))
        {
            if (debugLogs) Debug.Log($"MapConsumable '{name}': Collision com '{collision.collider.name}' ignorado (tag {collision.collider.tag}).");
            return;
        }
        TryDrop();
    }

    // Disparado a cada frame enquanto a colis�o persiste
    private void OnCollisionStay2D(Collision2D collision)
    {
        // Fallback caso o objeto j� comece encostado
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
        // Sem tabela de drops n�o h� o que fazer
        if (drops == null || drops.Length == 0) return;

        var chosen = GetWeightedRandomDrop();
        if (chosen != null && chosen.prefab != null)
        {
            // Instancia exatamente UM item na posi��o do arbusto + offset
            Vector3 spawnPos = transform.position + (Vector3)chosen.spawnOffset;
            Instantiate(chosen.prefab, spawnPos, Quaternion.identity);
            if (debugLogs) Debug.Log($"MapConsumable '{name}': Dropou '{chosen.prefab.name}' em {spawnPos}.");
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"MapConsumable '{name}': Nenhum drop v�lido encontrado.");
        }

        // Marca que j� dropou (se onlyOnce estiver ativo, impedir� futuras execu��es)
        hasDropped = true;

        // Destr�i o arbusto ap�s o drop, se configurado
        if (destroyAfterDrop)
            Destroy(gameObject, destroyDelay);
    }

    // Escolhe uma entrada de drop com base em pesos (roleta viciada)
    private DropEntry GetWeightedRandomDrop()
    {
        // Soma dos pesos v�lidos (> 0)
        float totalWeight = 0f;
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d != null && d.weight > 0f)
                totalWeight += d.weight;
        }
        if (totalWeight <= 0f) return null; // Todos os pesos s�o 0 ou a lista est� inv�lida

        // Escolhe um n�mero no intervalo [0, totalWeight] e percorre acumulando
        float r = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d == null || d.weight <= 0f) continue;
            cumulative += d.weight;
            // Quando o acumulado ultrapassa o n�mero sorteado, encontramos o drop
            if (r <= cumulative)
                return d;
        }
        // Fallback defensivo (n�o deve ocorrer, mas evita null)
        return drops[drops.Length - 1];
    }
}
