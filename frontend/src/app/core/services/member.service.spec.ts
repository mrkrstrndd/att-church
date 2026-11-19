import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MemberService } from './member.service';
import { MemberQrCode } from '../models/member.model';

describe('MemberService QR endpoints', () => {
  let service: MemberService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(MemberService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads and rotates a member QR code', () => {
    const qr: MemberQrCode = {
      memberId: 'member-1',
      memberName: 'Juan Dela Cruz',
      payload: 'church-attendance://member/abc'
    };

    service.getQrCode('member-1').subscribe((value) => expect(value).toEqual(qr));
    const load = http.expectOne('/api/members/member-1/qr-code');
    expect(load.request.method).toBe('GET');
    load.flush(qr);

    const rotated = { ...qr, payload: 'church-attendance://member/def' };
    service.rotateQrCode('member-1').subscribe((value) => expect(value).toEqual(rotated));
    const rotate = http.expectOne('/api/members/member-1/qr-code/rotate');
    expect(rotate.request.method).toBe('POST');
    rotate.flush(rotated);
  });

  it('loads VIP matches and inviter options', () => {
    const matches = [
      {
        id: 'vip-1',
        firstName: 'Vera',
        lastName: 'Visitor',
        fullName: 'Vera Visitor',
        email: 'vera@example.com',
        phoneNo: null,
        invitedByName: null
      }
    ];

    service.lookupVips('Vis').subscribe((value) => expect(value).toEqual(matches));
    const lookup = http.expectOne(
      (request) =>
        request.url === '/api/members/vip-lookup' &&
        request.params.get('lastName') === 'Vis'
    );
    expect(lookup.request.method).toBe('GET');
    lookup.flush(matches);

    const inviters = [{ id: 'member-1', fullName: 'Juan Dela Cruz', cellName: 'North Cell' }];
    service.inviterOptions('Juan').subscribe((value) => expect(value).toEqual(inviters));
    const options = http.expectOne(
      (request) =>
        request.url === '/api/members/inviter-options' &&
        request.params.get('search') === 'Juan'
    );
    expect(options.request.method).toBe('GET');
    options.flush(inviters);
  });
});
