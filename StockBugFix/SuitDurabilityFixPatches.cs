/*
 * Copyright 2026 Peter Han
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using HarmonyLib;
using System.Collections.Generic;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Applied to Equipment to make the Suit Sustainability Training skill (the ExosuitDurability
	/// perk) actually slow suit durability loss.
	///
	/// The base game already contains the intended logic in Equipment.Unequip: for a perk holder it
	/// pushes Durability.TimeEquipped forward by SUIT_DURABILITY_SKILL_BONUS (25%) of the time worn,
	/// so the wear computed in Durability.OnUnequipped is 25% smaller. But that block is gated behind
	/// "!slot.IsUnassigning()", and at the moment the wear is actually applied that flag is always
	/// true: the unequip arrives via slot.Unassign() -> Equippable.Unassign() -> Equipment.Unequip(),
	/// and slot.Unassign() holds "unassigning" true for the whole nested call (only resetting it after
	/// the wear has already been triggered). So the refund never reaches the wear calculation and the
	/// skill does nothing - for ordinary Duplicants AND for Bionics, which can gain the same perk from
	/// the "Skilled Worker" suits booster (it lands in MinionResume.AdditionalGrantedSkillPerkIDs, so
	/// MinionResume.HasPerk sees it just like a learned skill).
	///
	/// This prefix applies the same refund the base game intended, but before the unequip body runs -
	/// i.e. before slot.Unassign() triggers the wear - so it is in effect when Durability.OnUnequipped
	/// computes the loss. A small reentrancy guard prevents the refund from being applied twice on the
	/// re-entrant Equipment.Unequip call. The base game's own (now harmlessly post-wear) refund block
	/// is left alone; on a perk holder it just shifts an about-to-be-reset TimeEquipped.
	/// </summary>
	[HarmonyPatch(typeof(Equipment), nameof(Equipment.Unequip))]
	public static class Equipment_Unequip_SuitDurability_Patch {
		/// <summary>
		/// Equippables whose durability refund has already been applied during the current (possibly
		/// re-entrant) unequip, so it is not double counted.
		/// </summary>
		private static readonly ISet<Durability> REFUNDING = new HashSet<Durability>();

		/// <summary>
		/// Applied before Unequip runs.
		/// </summary>
		internal static void Prefix(Equipment __instance, Equippable equippable) {
			if (equippable == null || !equippable.TryGetComponent(out Durability durability))
				return;
			var target = __instance.GetTargetGameObject();
			if (target == null || !target.TryGetComponent(out MinionResume resume) || !resume.
					HasPerk(Db.Get().SkillPerks.ExosuitDurability.Id))
				return;
			// Reentrancy: slot.Unassign() inside Unequip re-enters Unequip for the same item; only
			// refund once, before the wear in Durability.OnUnequipped is triggered.
			if (REFUNDING.Add(durability)) {
				float worn = GameClock.Instance.GetTimeInCycles() - durability.TimeEquipped;
				if (worn > 0.0f)
					durability.TimeEquipped += worn * TUNING.EQUIPMENT.SUITS.
						SUIT_DURABILITY_SKILL_BONUS;
			}
		}

		/// <summary>
		/// Applied after Unequip runs.
		/// </summary>
		internal static void Postfix(Equippable equippable) {
			if (equippable != null && equippable.TryGetComponent(out Durability durability))
				REFUNDING.Remove(durability);
		}
	}
}
