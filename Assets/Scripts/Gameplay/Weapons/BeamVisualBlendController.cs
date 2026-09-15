using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponController))]
public sealed class BeamVisualBlendController : MonoBehaviour
{
    private const float BaseTransitionDuration = 1f;

    private readonly List<BeamVisualGroup> beamGroups = new();
    private WeaponController weaponController;
    private float sequenceTime;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
    }

    public void SetWeapons(IReadOnlyList<Weapon> weapons)
    {
        SetAllBeamAlphas(1f);
        beamGroups.Clear();
        sequenceTime = 0f;

        if (weapons == null)
            return;

        for (int index = 0; index < weapons.Count; index++)
        {
            if (weapons[index] is not ContinuousBeamWeapon beam)
                continue;

            AddBeam(beam);
        }

        ApplyCurrentBlend();
    }

    private void LateUpdate()
    {
        RemoveMissingBeams();
        if (beamGroups.Count == 0)
            return;

        if (beamGroups.Count == 1)
        {
            beamGroups[0].SetAlpha(1f);
            return;
        }

        float transitionRate = weaponController != null
            ? weaponController.BeamVisualTransitionRate
            : 1f;
        sequenceTime += Time.deltaTime * Mathf.Max(0f, transitionRate)
            / BaseTransitionDuration;
        sequenceTime = Mathf.Repeat(sequenceTime, beamGroups.Count);

        ApplyCurrentBlend();
    }

    private void AddBeam(ContinuousBeamWeapon beam)
    {
        for (int index = 0; index < beamGroups.Count; index++)
        {
            BeamVisualGroup group = beamGroups[index];
            if (!group.Matches(beam))
                continue;

            group.Add(beam);
            return;
        }

        BeamVisualGroup newGroup = new BeamVisualGroup(beam);
        newGroup.Add(beam);
        beamGroups.Add(newGroup);
    }

    private void ApplyCurrentBlend()
    {
        if (beamGroups.Count == 0)
            return;

        if (beamGroups.Count == 1)
        {
            beamGroups[0].SetAlpha(1f);
            return;
        }

        int currentIndex = Mathf.FloorToInt(sequenceTime) % beamGroups.Count;
        int nextIndex = (currentIndex + 1) % beamGroups.Count;
        float progress = sequenceTime - Mathf.Floor(sequenceTime);

        for (int index = 0; index < beamGroups.Count; index++)
        {
            float alpha = index == currentIndex
                ? 1f - progress
                : index == nextIndex
                    ? progress
                    : 0f;
            beamGroups[index].SetAlpha(alpha);
        }
    }

    private void RemoveMissingBeams()
    {
        for (int groupIndex = beamGroups.Count - 1;
             groupIndex >= 0;
             groupIndex--)
        {
            BeamVisualGroup group = beamGroups[groupIndex];
            group.RemoveMissingBeams();
            if (group.Count == 0)
                beamGroups.RemoveAt(groupIndex);
        }

        if (beamGroups.Count > 0)
            sequenceTime = Mathf.Repeat(sequenceTime, beamGroups.Count);
    }

    private void SetAllBeamAlphas(float alpha)
    {
        for (int index = 0; index < beamGroups.Count; index++)
            beamGroups[index].SetAlpha(alpha);
    }

    private sealed class BeamVisualGroup
    {
        private readonly List<ContinuousBeamWeapon> beams = new();

        public int Count => beams.Count;
        private readonly WeaponData weaponData;
        private readonly System.Type fallbackType;

        public BeamVisualGroup(ContinuousBeamWeapon beam)
        {
            weaponData = beam.weaponData;
            fallbackType = weaponData == null ? beam.GetType() : null;
        }

        public bool Matches(ContinuousBeamWeapon beam)
        {
            if (weaponData != null)
                return beam.weaponData == weaponData;

            return beam.weaponData == null && beam.GetType() == fallbackType;
        }

        public void Add(ContinuousBeamWeapon beam)
        {
            if (beam != null && !beams.Contains(beam))
                beams.Add(beam);
        }

        public void SetAlpha(float alpha)
        {
            for (int index = 0; index < beams.Count; index++)
            {
                ContinuousBeamWeapon beam = beams[index];
                if (beam != null)
                    beam.SetBeamVisualAlpha(alpha);
            }
        }

        public void RemoveMissingBeams()
        {
            for (int index = beams.Count - 1; index >= 0; index--)
            {
                if (beams[index] == null)
                    beams.RemoveAt(index);
            }
        }
    }
}
