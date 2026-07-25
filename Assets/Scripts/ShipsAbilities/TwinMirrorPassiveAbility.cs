using UnityEngine;
using Zenject;

public sealed class TwinMirrorPassiveAbility : PassiveAbility
{
    [InjectOptional] private DiContainer container;

    [SerializeField] private GameObject mirrorPrefabOverride;
    [SerializeField] private bool copyRotation = true;

    private TwinCloneController mirror;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void On()
    {
        base.On();

        if (owner == null)
            owner = GetComponent<ParentShip>();
        if (owner == null)
            return;

        EnsureMirror();

        if (mirror == null)
            return;

        mirror.gameObject.SetActive(true);
        mirror.Configure(owner, container, false, 0f, 0f);
        mirror.MirrorPositionFrom(owner.transform);
        CopyRotation();
    }

    public override void Off()
    {
        base.Off();

        if (mirror != null)
            mirror.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!isActive || owner == null || mirror == null || !mirror.gameObject.activeInHierarchy)
            return;

        mirror.MirrorPositionFrom(owner.transform);
        CopyRotation();
    }

    private void EnsureMirror()
    {
        if (mirror != null)
            return;

        GameObject instance = mirrorPrefabOverride != null
            ? Instantiate(mirrorPrefabOverride)
            : new GameObject($"{owner.name}_MirrorTwin");

        if (container != null)
            container.InjectGameObject(instance);

        mirror = instance.GetComponent<TwinCloneController>();
        if (mirror == null)
            mirror = instance.AddComponent<TwinCloneController>();
    }

    private void CopyRotation()
    {
        if (!copyRotation || owner == null || mirror == null)
            return;

        mirror.transform.rotation = owner.transform.rotation;
    }

    private void OnDestroy()
    {
        if (mirror != null)
            Destroy(mirror.gameObject);
    }
}
