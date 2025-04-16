import {ITemplateItem} from "@spt/models/eft/common/tables/ITemplateItem";
import {ItemHelper} from "@spt/helpers/ItemHelper";
import {ILogger} from "@spt/models/spt/utils/ILogger";
import {DatabaseService} from "@spt/services/DatabaseService";
import {JsonFileService} from "./JsonFileService";
import {WeaponAudioData} from "../Entity/IWeaponAudioData";

const WEAPON = "5422acb9af1c889c16000029"

export class FireRateService {
    private readonly logger: ILogger;
    private readonly itemHelper: ItemHelper;
    private readonly dataService: DatabaseService;
    private readonly jsonFileService: JsonFileService;

    constructor(logger: ILogger, dataService: DatabaseService, itemHelper: ItemHelper) {
        this.logger = logger;
        this.itemHelper = itemHelper;
        this.dataService = dataService;
        this.jsonFileService = new JsonFileService(logger);
    }

    public fireRateChange(): void {
        const templates = this.dataService.getTemplates();
        const items = templates?.items;

        if (!templates || !items) {
            this.logger.debug("[FireRateSound] Invalid dataService structure. Modification aborted");
            return;
        }

        const jsonData: { fileName: string; json: any }[] = this.jsonFileService.loadJsonFiles();
        this.resetModFlags(jsonData)
        const weaponsSPT: ITemplateItem[] = Object.values(items).filter(
            item => item?._id && this.itemHelper.isOfBaseclass(item._id, WEAPON)
        );

        this.applyModification(jsonData, weaponsSPT);

    }

private applyModification(jsonData: { fileName: string; json: any }[], weaponsSPT: ITemplateItem[]): void {
    for (const { fileName, json } of jsonData) {
        if (!json || typeof json !== "object") {
            this.logger.debug(`[FireRateSound] Invalid or missing JSON data in ${fileName}`);
            continue;
        }

        let modified = false;

        for (const id of Object.keys(json)) {
            const weaponJson = new WeaponAudioData(json[id]);
            const weaponSPT = weaponsSPT.find(w => w._id === id);

            if (!weaponSPT) {
                this.logger.debug(`[FireRateSound] ❌ Weapon not found in SPT: ID ${id}, name: ${weaponJson.shortName}`);
                continue;
            }

            if (!weaponSPT._props || typeof weaponSPT._props.bFirerate !== "number") {
                this.logger.debug(`[FireRateSound] ❌ Weapon has no valid bFirerate: ID ${id}, name: ${weaponJson.shortName}`);
                continue;
            }

            const currentRate = weaponSPT._props.bFirerate;
            const jsonRateMod = weaponJson.fireRateMod;

            // 🔍 mod = true if change
            if (currentRate !== jsonRateMod) {
                this.logger.debug(`[FireRateSound] FireRate mismatch for ${weaponJson.shortName} (${id}): SPT = ${currentRate}, JSON = ${jsonRateMod}`);
                weaponSPT._props.bFirerate = jsonRateMod;
                json[id]["mod"] = true;
                modified = true;

            }
        }

        if (modified) {
            this.logger.debug(`[FireRateSound] ✍️ Writing modified fireRate flags to ${fileName}`);
            this.jsonFileService.writeModifiedFireRateJson(fileName, json);
        }
    }
}

    private resetModFlags(jsonData: { fileName: string; json: any }[]): void {
        for (const {fileName, json} of jsonData) {
            if (!json || typeof json !== "object") {
                this.logger.debug(`[FireRateSound] JSON invalide : ${fileName}`);
                continue;
            }

            for (const id of Object.keys(json)) {
                if (json[id].mod === true) {
                    json[id].mod = false;
                }
            }
            this.jsonFileService.writeModifiedFireRateJson(fileName, json);

        }
    }
}

