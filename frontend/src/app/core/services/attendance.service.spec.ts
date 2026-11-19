import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AttendanceService } from './attendance.service';
import { ScanCheckInResult } from '../models/attendance.model';

describe('AttendanceService QR scanning', () => {
  let service: AttendanceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AttendanceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the scanned payload to the scan endpoint', () => {
    const result: ScanCheckInResult = {
      memberId: 'member-1',
      memberName: 'Juan Dela Cruz',
      alreadyCheckedIn: false,
      checkedInCount: 12
    };

    service.scanQrCode('event-1', 'day-1', 'church-attendance://member/abc').subscribe((value) => {
      expect(value).toEqual(result);
    });

    const request = http.expectOne('/api/events/event-1/days/day-1/attendance/scan');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      qrCode: 'church-attendance://member/abc'
    });
    request.flush(result);
  });
});
