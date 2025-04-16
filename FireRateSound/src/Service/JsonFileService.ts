import * as fs from "fs";
import * as path from "path";
import {ILogger} from "@spt/models/spt/utils/ILogger";
import {config} from "../config";


export class JsonFileService {
    private readonly jsonWeaponFolderPath: string;
    private readonly logger: ILogger;

    constructor(logger: ILogger) {
        this.jsonWeaponFolderPath = config.jsonWeaponFolderPath;
        this.logger = logger;
    }

    /**
     * Checks if the directory exists.
     * @returns `true` if the folder exists, `false` otherwise.
     */
    private doesFolderExist(folderPath: string): boolean {
        if (!fs.existsSync(folderPath)) {
            this.logger.debug(`[FireRateSound] [JsonFileService] Folder not found: ${folderPath}`);
            return false;
        }
        return true;
    }

     public getWeaponFolderPath(): string {
        return this.jsonWeaponFolderPath;
    }

 public loadJsonFiles<T>(): Array<{ fileName: string; json: T }> {
        if (!fs.existsSync(this.jsonWeaponFolderPath)) {
            this.logger.debug(`[FireRateSound] [JsonFileService] Folder not found: ${this.jsonWeaponFolderPath}`);
            return [];
        }

        try {
            const files = fs.readdirSync(this.jsonWeaponFolderPath);
            const jsonFiles = files.filter(file => file.includes("fireRates.json"));

            return jsonFiles.map(file => {
                const filePath = path.join(this.jsonWeaponFolderPath, file);
                const rawData = fs.readFileSync(filePath, "utf-8");
                return { fileName: file, json: JSON.parse(rawData) };
            });
        } catch (error) {
            this.logger.debug(`[FireRateSound] Error reading directory: ${error.message}`);
            return [];
        }
    }

    /**
     * Write fire rate diff results to firerate.json
     * @param json - The object to write
     * @param fileName - name file
     */
    public writeModifiedFireRateJson(fileName: string, json: any): void {
        const fullPath = path.join(this.jsonWeaponFolderPath, fileName);
        try {
            fs.writeFileSync(fullPath, JSON.stringify(json, null, 2), "utf-8");
            this.logger.debug(`[FireRateSound] Fire rate modification saved to ${fileName}`);
        } catch (error) {
            this.logger.debug(`[FireRateSound] Failed to write ${fileName}: ${error.message}`);
        }
    }
}
