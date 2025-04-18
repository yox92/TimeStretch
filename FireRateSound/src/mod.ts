import {DependencyContainer} from "tsyringe";
import type {ILogger} from "@spt/models/spt/utils/ILogger";
import {IPostDBLoadMod} from "@spt/models/external/IPostDBLoadMod";
import {DatabaseService} from "@spt/services/DatabaseService";
import {ItemHelper} from "@spt/helpers/ItemHelper";
import {PreSptModLoader} from "@spt/loaders/PreSptModLoader";
import {IPostSptLoadMod} from "@spt/models/external/IPostSptLoadMod";
import {FireRateService} from "./Service/FireRateService";


class FireRateSound implements IPostDBLoadMod, PreSptModLoader, IPostSptLoadMod {

    /**
     * Initializes the module and registers the dependency container.
     * @param container The instance of the dependency container.
     */
    public postDBLoad(container: DependencyContainer): void {
        const dataService: DatabaseService = container.resolve<DatabaseService>("DatabaseService")
        const logger: ILogger = container.resolve<ILogger>("WinstonLogger");
        const itemHelper: ItemHelper = container.resolve<ItemHelper>("ItemHelper");

         const firerateService = new FireRateService(logger,dataService,  itemHelper);

        firerateService.fireRateChange();

    }

}

module.exports = {mod: new FireRateSound()};
