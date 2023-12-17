import { Component, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { _routes } from 'app/config/routes';
import { DatasetService } from 'app/shared/services/dataset.service';
import { HttpEventType } from '@angular/common/http';
import { BaseComponent } from 'app/shared/base/base.component';
import { PopupService } from 'app/shared/services/popup.service';
import { AccountService } from 'app/shared/services/account.service';
import { EventService } from 'app/shared/services/event.service';

@Component({
  selector: 'app-submission-success',
  templateUrl: './submission-success.component.html',
  styleUrls: ['./submission-success.component.scss'],
})
export class SubmissionSuccessComponent
  extends BaseComponent
  implements OnInit
{
  public serialId: string;
  public submissionDate: string;
  protected reportId: string;
  protected workflowId: string;

  constructor(
    protected router: Router,
    protected _location: Location,
    protected popupService: PopupService,
    protected accountService: AccountService,
    protected eventService: EventService,
    protected datasetService: DatasetService
  ) {
    super(router, _location, popupService, accountService, eventService);
    const navigation = this.router.getCurrentNavigation();

    if (navigation !== null && navigation.extras.state !== undefined) {
      const { serialId, currentDate, reportId, workflowId } = navigation.extras
        .state as {
        serialId: string;
        currentDate: string;
        reportId: string;
        workflowId: string;
      };
      this.serialId = serialId;
      this.submissionDate = currentDate;
      this.reportId = reportId;
      this.workflowId = workflowId;
    }
  }

  ngOnInit(): void {
    if (!this.serialId) {
      this.router.navigate([_routes.security.landing]);
    }
  }

  goBack(): void {
    this.router.navigate([_routes.security.landing]);
  }

  downloadSummary(): void {
    this.datasetService
      .downloadSummary(this.workflowId, Number(this.reportId))
      .subscribe(
        data => {
          switch (data.type) {
            case HttpEventType.Response:
              const downloadedFile = new Blob([data.body], {
                type: data.body.type,
              });
              const a = document.createElement('a');

              a.setAttribute('style', 'display:none;');
              document.body.appendChild(a);
              a.download = '';
              a.href = URL.createObjectURL(downloadedFile);
              a.target = '_blank';
              a.click();
              document.body.removeChild(a);
              break;
          }
        },
        error => {
          console.log(error);
        }
      );
  }
}
