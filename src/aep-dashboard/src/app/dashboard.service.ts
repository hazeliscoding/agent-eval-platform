import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DashboardDataset } from './dashboard.models';

/// Loads the dataset the platform's generator / `aep compare` wrote to public/data/.
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  load(): Observable<DashboardDataset> {
    return this.http.get<DashboardDataset>('data/dataset.json');
  }
}
