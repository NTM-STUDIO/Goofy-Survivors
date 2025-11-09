using UnityEngine;
using System.Collections.Generic;

// Lives on the player; instantiates and attaches rune effect instances and detaches them on destroy.
public class RuneRuntime : MonoBehaviour
{
    private readonly List<RuneEffect> _instances = new List<RuneEffect>();
    private PlayerStats _owner;

    public void Initialize(PlayerStats owner, IEnumerable<RuneDefinition> runeDefs)
    {
        _owner = owner;
        if (runeDefs == null) return;
        foreach (var def in runeDefs)
        {
            if (def == null || def.effects == null) continue;
            foreach (var effect in def.effects)
            {
                if (effect == null) continue;
                // Instantiate a runtime copy so per-run state can be stored if needed
                var inst = ScriptableObject.Instantiate(effect);
                _instances.Add(inst);
                inst.OnAttach(_owner);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var inst in _instances)
        {
            if (inst != null) inst.OnDetach();
        }
        _instances.Clear();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _instances.Count; i++)
        {
            var inst = _instances[i];
            if (inst != null) inst.OnTick(dt);
        }
    }
}
