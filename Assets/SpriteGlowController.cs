using UnityEngine;

// [ExecuteAlways] faz a mesma coisa que ExecuteInEditMode, mas é mais moderno
[ExecuteAlways] 
public class SpriteGlowController : MonoBehaviour
{
    [Header("Bloom Geral")]
    [Min(0)] public float bloomIntensity = 1f;

    [Header("Emissão (Glow Interno)")]
    [ColorUsage(true, true)] // Permite cor HDR
    public Color emissionColor = Color.black;
    
    [Tooltip("Multiplicador extra para a força da emissão")]
    [Min(0)] public float emissionPower = 1f; 

    // Referências internas
    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _propBlock;

    // --- IMPORTANTE: Verifique se estes nomes batem com o "Reference" no seu Shader Graph ---
    private static readonly int BloomRef = Shader.PropertyToID("_BloomIntensity");
    private static readonly int EmColorRef = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmPowerRef = Shader.PropertyToID("_EmissionPower");

    void OnEnable()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _propBlock = new MaterialPropertyBlock();
        UpdateGlow();
    }

    void OnValidate()
    {
        // Atualiza instantaneamente ao mudar valores no Inspector
        UpdateGlow();
    }

    void Update()
    {
        // Descomente a linha abaixo se você estiver animando os valores via Animation ou Código em tempo real
        // UpdateGlow();
    }

    public void UpdateGlow()
    {
        if (_renderer == null) return;

        // 1. Pega o estado atual
        _renderer.GetPropertyBlock(_propBlock);

        // 2. Define os valores
        _propBlock.SetFloat(BloomRef, bloomIntensity);
        _propBlock.SetColor(EmColorRef, emissionColor);
        _propBlock.SetFloat(EmPowerRef, emissionPower);

        // 3. Aplica de volta
        _renderer.SetPropertyBlock(_propBlock);
    }
}