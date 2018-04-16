/*
 * Squidex Headless CMS
 *
 * @license
 * Copyright (c) Squidex UG (haftungsbeschränkt). All rights reserved.
 */

import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import '@app/framework/utils/rxjs-extensions';

import {
    DialogService,
    ImmutableArray,
    State,
    Version
} from '@app/framework';

import { AppsState } from './apps.state';
import { AuthService } from './../services/auth.service';

import {
    ChangePlanDto,
    PlanDto,
    PlansService
} from './../services/plans.service';

interface PlanInfo {
    plan: PlanDto;

    isYearlySelected?: boolean;
    isSelected?: boolean;
}

interface Snapshot {
    plans?: ImmutableArray<PlanInfo>;

    version?: Version;

    isOwner?: boolean;
    isDisabled?: boolean;

    hasPortal?: boolean;
}

@Injectable()
export class PlansState extends State<Snapshot> {
    public plans =
        this.changes.map(x => x.plans);

    public isOwner =
        this.changes.map(x => x.isOwner);

    public isDisabled =
        this.changes.map(x => x.isDisabled || !x.isOwner);

    public hasPortal =
        this.changes.map(x => x.hasPortal);

    public window = window;

    constructor(
        private readonly appsState: AppsState,
        private readonly authState: AuthService,
        private readonly dialogs: DialogService,
        private readonly plansService: PlansService
    ) {
        super({});
    }

    public load(notifyLoad = false, overridePlanId?: string): Observable<any> {
        return this.plansService.getPlans(this.appName)
            .do(dto => {
                if (notifyLoad) {
                    this.dialogs.notifyInfo('Plans reloaded.');
                }

                this.next(s => {
                    const planId = overridePlanId || dto.currentPlanId;

                    return {
                        ...s,
                        plans: ImmutableArray.of(dto.plans.map(x => this.createPlan(x, planId))),
                        isOwner: !dto.planOwner || dto.planOwner === this.userId,
                        isDisabled: false,
                        hasPortal: dto.hasPortal
                    };
                });
            })
            .notify(this.dialogs);
    }

    public change(planId: string): Observable<any> {
        this.next(s => ({ ...s, isDisabled: true }));

        return this.plansService.putPlan(this.appName, new ChangePlanDto(planId), this.version)
            .do(dto => {
                if (dto.payload.redirectUri && dto.payload.redirectUri.length > 0) {
                    this.window.location.href = dto.payload.redirectUri;
                } else {
                    this.next(s => {
                        return {
                            ...s,
                            plans: s.plans!.map(x => this.createPlan(x.plan, planId)),
                            isOwner: true,
                            isDisabled: false
                        };
                    });
                }
            })
            .notify(this.dialogs);
    }

    private createPlan(plan: PlanDto, id: string) {
        return { plan, isYearlySelected: plan.yearlyId === id, isSelected: plan.id === id };
    }

    private get appName() {
        return this.appsState.appName;
    }

    private get userId() {
        return this.authState.user!.id;
    }

    private get version() {
        return this.snapshot.version!;
    }
}

