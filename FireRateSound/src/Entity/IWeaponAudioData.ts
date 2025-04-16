// Interface correspondant à la nouvelle structure du JSON
export interface IWeaponAudioData {
    id: string;
    name: string;
    shortName: string;
    fireRateMod: number;
    fireRate: number;
    hasFullAuto: boolean;
    mod: boolean;
}

export class WeaponAudioData implements IWeaponAudioData {
    id: string;
    name: string;
    shortName: string;
    fireRateMod: number;
    fireRate: number;
    hasFullAuto: boolean;
    mod: boolean;

    constructor(data: any) {
        this.id = data.id ?? "";
        this.name = data.name ?? "";
        this.shortName = data.shortName ?? "";
        this.fireRateMod = data.fireRateMod ?? 0;
        this.fireRate = data.fireRate ?? 0;
        this.hasFullAuto = data.hasFullAuto ?? false;
        this.mod = data.mod ?? false;
    }

    toJson(): IWeaponAudioData {
        return {
            id: this.id,
            name: this.name,
            shortName: this.shortName,
            fireRateMod: this.fireRateMod,
            fireRate: this.fireRate,
            hasFullAuto: this.hasFullAuto,
            mod: this.mod
        };
    }
}
