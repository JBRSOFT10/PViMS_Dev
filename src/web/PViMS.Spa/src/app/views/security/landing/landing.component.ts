import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { _routes } from 'app/config/routes';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { AboutPopupComponent } from 'app/shared/components/about/about.popup.component';
import { environment } from 'environments/environment';

@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss'],
})
export class LandingComponent implements OnInit {
  dgdaLogo = '';
  pvimsLogo = '';
  usaidLogo = '';

  constructor(protected _router: Router, protected dialog: MatDialog) {
    let self = this;
    self.dgdaLogo = `assets/images/site_logo_${environment.countryISOCode}.png`;
    self.pvimsLogo = 'assets/images/pvims_logo_sm.png';
    self.usaidLogo = 'assets/images/usaid_blue_hands.png';
  }

  ngOnInit() {}

  navigateToSpontaneous(): void {
    let self = this;
    self._router.navigate([_routes.public.spontaneous]).then(() => {
      window.location.reload();
    });
  }

  navigateToLogin(): void {
    let self = this;
    self._router.navigate([_routes.security.login]);
  }

  openAboutPopUp(data: any = {}) {
    let self = this;
    let dialogRef: MatDialogRef<any> = self.dialog.open(AboutPopupComponent, {
      width: '920px',
      disableClose: true,
    });
    dialogRef.afterClosed().subscribe(res => {
      if (!res) {
        // If user press cancel
        return;
      }
    });
  }
}
