import { Component, OnInit, OnDestroy, ViewEncapsulation } from '@angular/core';
import { DatePipe, Location } from '@angular/common';
import { BaseComponent } from 'app/shared/base/base.component';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormBuilder,
  FormGroup,
  Validators,
  FormControl,
  FormArray,
  AbstractControl,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { PopupService } from 'app/shared/services/popup.service';
import { AccountService } from 'app/shared/services/account.service';
import { EventService } from 'app/shared/services/event.service';
import { MediaObserver, MediaChange } from '@angular/flex-layout';
import { Subscription } from 'rxjs';
// Depending on whether rollup is used, moment needs to be imported differently.
// Since Moment.js doesn't have a default export, we normally need to import using the `* as`
// syntax. However, rollup creates a synthetic default module and we thus need to import it using
// the `default as` syntax.
import * as _moment from 'moment';
import { egretAnimations } from 'app/shared/animations/egret-animations';
import { _routes } from 'app/config/routes';
import { MatDialogRef, MatDialog } from '@angular/material/dialog';
import { DatasetService } from 'app/shared/services/dataset.service';
import { distinctUntilChanged, finalize } from 'rxjs/operators';
import { DatasetCategoryViewModel } from 'app/shared/models/dataset/dataset-category-view.model';
import { DatasetElementSubViewModel } from 'app/shared/models/dataset/dataset-element-sub-view.model';
import { SpontaneousTablePopupComponent } from './spontaneous-table/spontaneous-table.popup.component';
import { v4 as uuidv4 } from 'uuid';
import { MatRadioChange } from '@angular/material/radio';
import { MatSelectChange } from '@angular/material/select';
import {
  MAT_MOMENT_DATE_FORMATS,
  MomentDateAdapter,
} from '@angular/material-moment-adapter';
import {
  DateAdapter,
  MAT_DATE_FORMATS,
  MAT_DATE_LOCALE,
} from '@angular/material/core';
import { MatDatepickerInputEvent } from '@angular/material/datepicker';

const moment = _moment;

@Component({
  templateUrl: './spontaneous.component.html',
  styleUrls: ['./spontaneous.component.scss'],
  encapsulation: ViewEncapsulation.None,
  animations: egretAnimations,
  providers: [
    // `MomentDateAdapter` and `MAT_MOMENT_DATE_FORMATS` can be automatically provided by importing
    // `MatMomentDateModule` in your applications root module. We provide it at the component level
    // here, due to limitations of our example generation script.
    {
      provide: DateAdapter,
      useClass: MomentDateAdapter,
      deps: [MAT_DATE_LOCALE],
    },
    { provide: MAT_DATE_FORMATS, useValue: MAT_MOMENT_DATE_FORMATS },
  ],
})
export class SpontaneousComponent
  extends BaseComponent
  implements OnInit, OnDestroy
{
  public datasetId = 1;
  public datasetCategories: DatasetCategoryViewModel[] = [];

  public viewModelForm: FormGroup;
  public formArray: FormArray;
  protected genderValue: string;
  protected adverseEventTreatment: string;
  public currentDate: string;
  protected isAgeNotAvailable: boolean;
  protected typeOfEvents: string[];
  protected eventInformation: string[];
  protected relevantHistory: string[];
  protected outcomeOfAdverseEvent: string;
  public divisionList: { selectedkey: string; selectedValue: string }[];
  public districtList: { selectedkey: string; selectedValue: string }[];
  public thanaList: { selectedkey: string; selectedValue: string }[];
  protected isMedDateNotAvailable: string;
  protected isEventDateNotAvailable: string;
  protected medEndDateMin: any;
  protected eventEndDateMin: any;
  protected sourceOfReport: string;
  protected reportType: string;

  constructor(
    protected _activatedRoute: ActivatedRoute,
    protected _router: Router,
    protected _location: Location,
    protected _formBuilder: FormBuilder,
    protected popupService: PopupService,
    protected accountService: AccountService,
    protected eventService: EventService,
    protected datasetService: DatasetService,
    protected dialog: MatDialog,
    protected mediaObserver: MediaObserver,
    protected datePipe: DatePipe
  ) {
    super(_router, _location, popupService, accountService, eventService);

    this.flexMediaWatcher = mediaObserver.media$.subscribe(
      (change: MediaChange) => {
        if (change.mqAlias !== this.currentScreenWidth) {
          this.currentScreenWidth = change.mqAlias;
          //this.setupTable();
        }
      }
    );
  }

  currentScreenWidth: string = '';
  flexMediaWatcher: Subscription;

  ngOnInit(): void {
    const self = this;
    const today = moment().format('DD-MM-YYYY');

    self.viewModelForm = this._formBuilder.group({
      formArray: this._formBuilder.array([]),
    });

    self.loadDataset();
    self.loadDivisions();
    self.adverseEventTreatment = 'Yes';
    self.currentDate = today;
    self.isAgeNotAvailable = true;
    self.typeOfEvents = [];
    self.eventInformation = [];
    self.relevantHistory = [];
    self.divisionList = [];
    self.districtList = [];
    self.thanaList = [];
    self.isMedDateNotAvailable = 'Yes';
    self.isEventDateNotAvailable = 'Yes';
    self.medEndDateMin = '';
  }

  ngAfterViewInit(): void {
    let self = this;
  }

  ngOnDestroy(): void {
    this._unsubscribeAll.next();
    this._unsubscribeAll.complete();
    this.eventService.removeAll(SpontaneousComponent.name);
  }

  loadDataset(): void {
    let self = this;
    self.setBusy(true);
    self.datasetService
      .getSpontaneousDataset()
      .pipe(finalize(() => self.setBusy(false)))
      .subscribe(
        result => {
          self.datasetCategories = result.datasetCategories;
          self.datasetId = result.id;

          self.prepareFormArray();
        },
        error => {
          self.handleError(error, error.statusText);
        }
      );
  }

  loadDivisions(): void {
    let self = this;
    self.setBusy(true);
    self.datasetService
      .loadDivisions()
      .pipe(finalize(() => self.setBusy(false)))
      .subscribe(
        result => {
          self.divisionList = result;
        },
        error => {
          self.handleError(error, error.statusText);
        }
      );
  }

  loadDistricts(divisionId: string): void {
    let self = this;
    self.setBusy(true);
    self.datasetService
      .loadDistrict(divisionId)
      .pipe(finalize(() => self.setBusy(false)))
      .subscribe(
        result => {
          self.districtList = result;
        },
        error => {
          self.handleError(error, error.statusText);
        }
      );
  }

  loadThanas(districtId: string): void {
    let self = this;
    self.setBusy(true);
    self.datasetService
      .loadThana(districtId)
      .pipe(finalize(() => self.setBusy(false)))
      .subscribe(
        result => {
          self.thanaList = result;
        },
        error => {
          self.handleError(error, error.statusText);
        }
      );
  }

  generateColumnArray(elementSubs: DatasetElementSubViewModel[]): string[] {
    let displayColumns: string[] = [];
    if (elementSubs.length > 5) {
      displayColumns = elementSubs
        .slice(0, 5)
        .map(a => a.datasetElementSubName);
      displayColumns.push('actions');
    } else {
      displayColumns = elementSubs.map(a => a.datasetElementSubName);
      displayColumns.push('actions');
    }
    return displayColumns;
  }

  openPopup(
    arrayIndex: number,
    rowIndex: number,
    datasetElementId: number,
    datasetElementSubs: DatasetElementSubViewModel[],
    data: any = {},
    isNew?
  ) {
    let self = this;
    let title = isNew ? 'Add Record' : 'Update Record';
    let dialogRef: MatDialogRef<any> = self.dialog.open(
      SpontaneousTablePopupComponent,
      {
        width: '920px',
        disableClose: true,
        data: { title: title, datasetElementSubs, payload: data },
      }
    );
    dialogRef.afterClosed().subscribe(res => {
      if (!res) {
        // If user press cancel
        return;
      }
      // Get existing value for the table element
      let tableValue = self.getTableValueFromArray(arrayIndex);

      // Prepare existing array of table values
      let tableRowsArray: any[] = [];
      if (Object.values(tableValue)[0] != null) {
        tableRowsArray = Object.assign([], Object.values(tableValue)[0]);
      }

      if (isNew) {
        tableRowsArray.push(res.elements);
      } else {
        tableRowsArray[rowIndex] = res.elements;
      }
      self.setTableValueArray(arrayIndex, datasetElementId, tableRowsArray);
    });
  }

  removeRecord(
    arrayIndex: number,
    rowIndex: number,
    datasetElementId: number
  ): void {
    let self = this;

    // Get existing value for the table element
    let tableValue = self.getTableValueFromArray(arrayIndex);

    // Prepare existing array of table values
    let tableRowsArray: any[] = [];
    if (Object.values(tableValue)[0] != null) {
      tableRowsArray = Object.assign([], Object.values(tableValue)[0]);
    }

    tableRowsArray.splice(rowIndex, 1);
    self.setTableValueArray(arrayIndex, datasetElementId, tableRowsArray);
  }

  prevent(event) {
    event.preventDefault();
  }

  saveForm(): void {
    if (
      confirm(
        'The information provided here are true to the best of my knowledge'
      ) == true
    ) {
      let self = this;
      self.setBusy(true);

      let allModels: any[] = [];

      const arrayControl = <FormArray>this.viewModelForm.controls['formArray'];
      arrayControl.controls.forEach(formGroup => {
        allModels.push(formGroup.value);
      });

      self.datasetService
        .saveSpontaneousInstance(self.datasetId, allModels)
        .subscribe(
          result => {
            self.notify('Report created successfully', 'Spontaneous Report');
            self._router.navigate([_routes.public.submissionSuccess], {
              state: {
                serialId: result.patient_identifier,
                currentDate: result.report_date,
                reportId: result.report_id,
                workflowId: '4096D0A3-45F7-4702-BDA1-76AEDE41B986',
              },
            });
          },
          error => {
            self.handleError(error, 'Error saving spontaneous report');
          }
        );
    }
  }

  getTableDataSource(arrayIndex: number): any[] {
    let self = this;
    let tableValue = self.getTableValueFromArray(arrayIndex);

    // Prepare existing array of table values
    let tableRowsArray: any[] = [];
    if (Object.values(tableValue)[0] != null) {
      tableRowsArray = Object.assign([], Object.values(tableValue)[0]);
    }

    return tableRowsArray;
  }

  formatOutput(outputValue: string): string {
    if (moment.isMoment(outputValue)) {
      return this.datePipe.transform(outputValue, 'yyyy-MM-dd');
    }
    return outputValue;
  }

  onChangeDivision(event: MatSelectChange) {
    this.loadDistricts(event.value);
  }

  onChangeDistrict(event: MatSelectChange) {
    this.loadThanas(event.value);
  }

  onChangeGender(event: MatSelectChange): void {
    this.genderValue = event.value;
  }

  onChangeTypeOfEvent(event: MatSelectChange): void {
    this.typeOfEvents = event.value;
  }

  onChangeEventInformation(event: MatSelectChange): void {
    this.eventInformation = event.value;
  }

  onChangeRelevantHistory(event: MatSelectChange): void {
    this.relevantHistory = event.value;
  }

  onChangeAdverseEventTreatment(event: MatRadioChange): void {
    this.adverseEventTreatment = event.value;
  }

  onChangeOutcomeOfAdverseEvent(event: MatRadioChange): void {
    this.outcomeOfAdverseEvent = event.value;
  }

  medStartDateChange(event: MatDatepickerInputEvent<Date>): void {
    this.medEndDateMin = new Date(event.value.toISOString());
  }

  eventStartDateChange(event: MatDatepickerInputEvent<Date>): void {
    this.eventEndDateMin = new Date(event.value.toISOString());
  }

  onChangeSourceOfReporting(event: MatRadioChange): void {
    this.sourceOfReport = event.value;
  }

  onChangeReportType(event: MatRadioChange): void {
    this.reportType = event.value;
  }

  phoneNumberValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value;

      if (!value) {
        return null;
      }

      // const isPhoneValid =
      //   value.toString().match(/^(?:880)?((01|1)[1,3-9]\d{8})$/) !== null;
      const isPhoneValid = value.toString().match(/^[\d ()+-]+$/) !== null;

      return !isPhoneValid ? { invalidPhone: true } : null;
    };
  }
  emailValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value;

      if (!value) {
        return null;
      }

      // const isPhoneValid =
      //   value.toString().match(/^(?:880)?((01|1)[1,3-9]\d{8})$/) !== null;
      const isEmailValid =
        value.toString().match('^[a-z0-9._%+-]+@[a-z0-9.-]+\\.[a-z]{2,4}$') !==
        null;

      return !isEmailValid ? { invalidEmail: true } : null;
    };
  }
  private prepareFormArray(): void {
    let self = this;
    self.datasetCategories.forEach((category, index) => {
      // add form group per category
      let newGroup = self.addGroupForCategory();
      let elements = newGroup.get('elements') as FormGroup;

      category.datasetElements.forEach(element => {
        // Add elements to form group
        let validators = [];
        if (element.required) {
          validators.push(Validators.required);
        }
        if (element.stringMaxLength != null) {
          validators.push(Validators.maxLength(element.stringMaxLength));
        }
        if (
          element.numericMinValue != null &&
          element.numericMaxValue != null
        ) {
          validators.push(Validators.max(element.numericMaxValue));
          validators.push(Validators.min(element.numericMinValue));
        }
        if (
          element.datasetElementId === 106 ||
          element.datasetElementId === 136
        ) {
          validators.push(this.phoneNumberValidator());
        }
        if (element.datasetElementId === 137) {
          validators.push(this.emailValidator());
        }
        elements.addControl(
          element.datasetElementId.toString(),
          new FormControl(null, validators)
        );
      });

      if (index === 0) {
        // Set AE Report number field
        const serialNumber = self.generateSerialId();
        elements.get('103').setValue(serialNumber);

        // Set Date received field
        elements.controls['104'].setValue(moment());

        // Pregnant field validation
        elements.get('110').valueChanges.subscribe(val => {
          if (val !== 'Female') {
            elements.controls['111'].clearValidators();
            elements.controls['111'].setValue(null);
          }
          elements.controls['111'].updateValueAndValidity();
        });

        /**
         * Age related fields' shenanigans
         */
        const $ageCheckBox = elements.get('307');
        const $ageYear = elements.get('107');
        const $ageMonth = elements.get('295');
        const $ageDay = elements.get('296');
        $ageYear.setValue(0);
        $ageMonth.setValue(0);
        $ageDay.setValue(0);
        $ageCheckBox.setValue(true);
        $ageCheckBox.valueChanges.subscribe(checked => {
          if (checked) {
            self.isAgeNotAvailable = true;
            $ageYear.setValue(0);
            $ageMonth.setValue(0);
            $ageDay.setValue(0);
            $ageYear.clearValidators();
            $ageMonth.clearValidators();
            $ageDay.clearValidators();
          } else {
            self.isAgeNotAvailable = false;
            $ageYear.setValidators(Validators.required);
            $ageYear.reset();
            $ageMonth.setValidators(Validators.required);
            $ageMonth.reset();
            $ageDay.setValidators(Validators.required);
            $ageDay.reset();
          }
          $ageYear.updateValueAndValidity();
          $ageMonth.updateValueAndValidity();
          $ageDay.updateValueAndValidity();
        });
      //}

      //if (index === 1) {
        const $adverseEventField = elements.get('125');
        const adverseEeventFieldValues = category.datasetElements.find(
          item => item.datasetElementId === 125
        ).selectionDataItems;
        const $actionTakenField = elements.get('127');
        const actionTakenFieldValues = category.datasetElements.find(
          item => item.datasetElementId === 127
        ).selectionDataItems;
        const $reactTionSubsideFiled = elements.get('128');
        const reactTionSubsideFiledValues = category.datasetElements.find(
          item => item.datasetElementId === 128
        ).selectionDataItems;
        const $reacttionAppearedField = elements.get('129');
        const reacttionAppearedFieldValues = category.datasetElements.find(
          item => item.datasetElementId === 128
        ).selectionDataItems;
        const $outcomesField = elements.get('131');
        const outcomesFieldValues = category.datasetElements.find(
          item => item.datasetElementId === 131
        ).selectionDataItems;
        const $medicationStartDateNotAvailable = elements.get('306');
        const $medicationStartDate = elements.get('116');
        const $medicationEndDate = elements.get('117');
        const $eventDateNotAvailable = elements.get('308');
        const $eventStartDate = elements.get('123');
        const $eventEndDate = elements.get('124');

        const radioButtonFields = [
          $actionTakenField,
          $reactTionSubsideFiled,
          $reacttionAppearedField,
        ];

        /**
         * Set radio button initial values
         * Though the UI shows the first value as selected
         * While submitting, we don't receive the value
         */
        $adverseEventField.setValue(adverseEeventFieldValues[0].selectionKey);
        $actionTakenField.setValue(actionTakenFieldValues[0].selectionKey);
        $reactTionSubsideFiled.setValue(
          reactTionSubsideFiledValues[0].selectionKey
        );
        $reacttionAppearedField.setValue(
          reacttionAppearedFieldValues[0].selectionKey
        );
        $outcomesField.setValue(outcomesFieldValues[0].selectionKey);

        // Set all radio buttons values when they are changed
        radioButtonFields.forEach(radioButton => {
          radioButton.valueChanges
            .pipe(distinctUntilChanged())
            .subscribe(val => radioButton.setValue(val));
        });

        // Handle $adverseEventField radio button change separately
        $adverseEventField.valueChanges
          .pipe(distinctUntilChanged())
          .subscribe(val => {
            $adverseEventField.setValue(val);
            // If yes, field validation
            if (val !== 'Yes') {
              elements.controls['126'].clearValidators();
              elements.controls['126'].setValue(null);
            }
            elements.controls['126'].updateValueAndValidity();
          });

        // Handle $outcomesField radio button change separately
        $outcomesField.valueChanges
          .pipe(distinctUntilChanged())
          .subscribe(val => {
            $outcomesField.setValue(val);
            // Date of death validation
            if (val !== 'Fatal') {
              elements.controls['156'].clearValidators();
              elements.controls['156'].setValue(null);
            }
            elements.controls['156'].updateValueAndValidity();
          });

        // If others, please specify validation
        elements
          .get('132')
          .valueChanges.pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val[0] !== 'Others (Please specify)') {
              elements.controls['153'].clearValidators();
              elements.controls['153'].setValue(null);
            }
            elements.controls['153'].updateValueAndValidity();
          });

        // If others, please specify(type of event) validation
        elements
          .get('112')
          .valueChanges.pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val[0] !== 'Others') {
              elements.controls['298'].clearValidators();
              elements.controls['298'].setValue(null);
            }
            elements.controls['298'].updateValueAndValidity();
          });

        // Event information (If others, please specify) validation
        elements
          .get('301')
          .valueChanges.pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val[0] !== 'Others') {
              elements.controls['302'].clearValidators();
              elements.controls['302'].setValue(null);
            }
            elements.controls['302'].updateValueAndValidity();
          });

        // Medication start date
        $medicationStartDateNotAvailable.setValue(true);
        if (self.isMedDateNotAvailable === 'Yes') {
          $medicationStartDate.clearValidators();
        }
        $medicationStartDateNotAvailable.valueChanges
          .pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val) {
              self.isMedDateNotAvailable = 'Yes';
              $medicationStartDate.clearValidators();
              $medicationEndDate.setValue(null);
              $medicationStartDate.setValue(null);
            } else {
              self.isMedDateNotAvailable = 'No';
              $medicationStartDate.setValidators(Validators.required);
            }

            $medicationStartDate.updateValueAndValidity();
          });

        // Event date
        $eventDateNotAvailable.setValue(true);
        if (self.isEventDateNotAvailable === 'Yes') {
          $eventStartDate.clearValidators();
          $eventEndDate.clearValidators();
        }
        $eventDateNotAvailable.valueChanges
          .pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val) {
              self.isEventDateNotAvailable = 'Yes';
              $eventStartDate.clearValidators();
              $eventEndDate.setValue(null);
              $eventStartDate.setValue(null);
            } else {
              self.isEventDateNotAvailable = 'No';
              $eventStartDate.setValidators(Validators.required);
            }

            $eventStartDate.updateValueAndValidity();
            $eventEndDate.updateValueAndValidity();
          });
     // }

      //if (index === 3) {
        // Set report submission date
        elements.get('140').setValue(moment());

        const $sourceOfReportingRadioField = elements.get('300');
        const sourceOfReportingRadioFieldValues = category.datasetElements.find(
          item => item.datasetElementId === 300
        ).selectionDataItems;
        const $reportingTypeRadioField = elements.get('303');

        // Set initial value of "Enter source of reporting" field
        $sourceOfReportingRadioField.setValue(
          sourceOfReportingRadioFieldValues[0].selectionKey
        );

        $sourceOfReportingRadioField.valueChanges
          .pipe(distinctUntilChanged())
          .subscribe(val => {
            if (val !== 'Marketing authorization holder') {
              self.reportType = '';
              elements.get('304').setValue(null);
              elements.get('305').setValue(null);
              elements.get('305').clearValidators();
            }
            if (val === 'Marketing authorization holder') {
              //self.reportType = this.reportType;
              elements.get('152').setValue(null);
              elements.get('152').clearValidators();
              /*elements.get('303').updateValueAndValidity();*/
            }
            elements.get('152').updateValueAndValidity();
            elements.get('304').updateValueAndValidity();
            elements.get('305').updateValueAndValidity();
          });
      }
    });
  }

  private getTableValueFromArray(index: number): any {
    let self = this;

    const arrayControl = <FormArray>self.viewModelForm.controls['formArray'];
    let formGroup = arrayControl.controls[index] as FormGroup;
    let elements = formGroup.get('elements') as FormGroup;

    return elements.value;
  }

  private setTableValueArray(
    index: number,
    datasetElementId: number,
    value: any[]
  ): void {
    let self = this;

    const arrayControl = <FormArray>self.viewModelForm.controls['formArray'];
    let formGroup = arrayControl.controls[index] as FormGroup;
    let elements = formGroup.get('elements') as FormGroup;
    let control = elements.get(datasetElementId.toString()) as FormControl;
    if (control) {
      control.setValue(value);
    }
  }

  private addGroupForCategory(): FormGroup {
    const arrayControl = <FormArray>this.viewModelForm.controls['formArray'];
    let newGroup = this._formBuilder.group({
      elements: this._formBuilder.group([]),
    });
    arrayControl.push(newGroup);
    return newGroup;
  }

  private generateSerialId(): string {
    const year = moment().format('YYYY');
    const uuid = uuidv4();
    const serial = `DGDA_BD_${year}_${uuid}`;

    return serial;
  }
}
