import { Routes } from '@angular/router';
import { SpontaneousComponent } from './spontaneous/spontaneous.component';
import { SubmissionSuccessComponent } from './submission-success/submission-success.component';

export const PublicRoutes: Routes = [
  {
    path: 'spontaneous',
    component: SpontaneousComponent,
    data: { title: 'Public Spontaneous Report' },
  },
  {
    path: 'submission-success',
    component: SubmissionSuccessComponent,
    data: { title: 'Success' },
  },
];
