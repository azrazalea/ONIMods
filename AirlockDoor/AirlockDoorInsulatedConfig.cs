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

using System;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// An airlock door that requires power, but allows Duplicants to pass without ever
	/// transmitting liquid or gas (unless set to Open). This version is a much better
	/// temperature insulator than the default Airlock Door, but requires late game technology.
	/// </summary>
	public sealed class AirlockDoorInsulatedConfig : IBuildingConfig {
		public const string ID = "PAirlockDoorInsulated";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding AirlockDoorInsulatedTemplate;

		/// <summary>
		/// Creates this building.
		/// </summary>
		/// <returns>The building prototype.</returns>
		internal static PBuilding CreateBuilding() {
			return AirlockDoorInsulatedTemplate = new PBuilding(ID, AirlockDoorStrings.BUILDINGS.
					PREFABS.PAIRLOCKDOORINSULATED.NAME) {
				AddAfter = InsulatedDoorConfig.ID,
				Animation = "airlock_door_insulated_kanim",
				Category = "Base",
				ConstructionTime = 90.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER2,
				Description = null, EffectText = null,
				Entombs = false,
				Floods = false,
				Height = 2,
				HP = 40,
				LogicIO = {
					LogicPorts.Port.InputPort(AirlockDoor.OPEN_CLOSE_PORT_ID, CellOffset.none,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_ACTIVE,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_INACTIVE)
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.INSULATOR, tier: 4),
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 2),
				},
				// Overheating is not possible on solid tile buildings because they bypass
				// structure temperatures so sim will never send the overheat notification
				Placement = BuildLocationRule.Tile,
				// Recharge doubled 120->240 W to match the base Airlock Door (shares the 3000/use
				// energy cost, so it had the same stall). (local fix)
				PowerInput = new PowerRequirement(240.0f, new CellOffset(0, 0)),
				RotateMode = PermittedRotations.Unrotatable,
				SceneLayer = Grid.SceneLayer.InteriorWall,
				SubCategory = "doors",
				Tech = "Catalytics",
				Width = 3
			};
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			AirlockDoorInsulatedTemplate?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			if (AirlockDoorInsulatedTemplate == null)
				throw new ArgumentNullException(nameof(AirlockDoorInsulatedTemplate));
			var def = AirlockDoorInsulatedTemplate.CreateDef();
			def.ForegroundLayer = Grid.SceneLayer.TileMain;
			def.IsFoundation = true;
			def.PreventIdleTraversalPastBuilding = true;
			// /50 multiplier to thermal conductivity
			def.ThermalConductivity = 0.02f;
			def.TileLayer = PGameUtils.GetObjectLayer(nameof(ObjectLayer.FoundationTile),
				ObjectLayer.FoundationTile);
			return def;
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			AirlockDoorInsulatedTemplate?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			AirlockDoorInsulatedTemplate?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			AirlockDoorInsulatedTemplate?.DoPostConfigureComplete(go);
			AirlockDoorInsulatedTemplate?.CreateLogicPorts(go);
			var ad = go.AddOrGet<AirlockDoor>();
			ad.EnergyCapacity = AirlockDoorConfig.ENERGY_CAPACITY;
			ad.EnergyPerUse = AirlockDoorConfig.ENERGY_PER_USE;
			var occupier = go.AddOrGet<SimCellOccupier>();
			occupier.doReplaceElement = true;
			occupier.notifyOnMelt = true;
			go.AddOrGet<TileTemperature>();
			go.AddOrGet<AccessControl>().controlEnabled = true;
			go.AddOrGet<KBoxCollider2D>();
			go.AddOrGet<BuildingHP>().destroyOnDamaged = true;
			Prioritizable.AddRef(go);
			go.AddOrGet<CopyBuildingSettings>().copyGroupTag = GameTags.Door;
			go.AddOrGet<Workable>().workTime = 3f;
			if (go.TryGetComponent(out BuildingEnabledButton button))
				Object.DestroyImmediate(button);
			if (go.TryGetComponent(out KBatchedAnimController kbac))
				kbac.initialAnim = "closed";
		}
	}
}
