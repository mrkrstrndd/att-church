import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/services/auth.service';
import { AuthResponse } from '../../core/models/auth.model';

describe('LoginComponent', () => {
  const response: AuthResponse = {
    accessToken: 'access',
    refreshToken: 'refresh',
    expiresAt: '2026-11-05T12:00:00Z',
    user: {
      id: 'user-1',
      memberId: 'member-1',
      username: 'admin',
      displayName: 'System Admin',
      firstName: 'System',
      lastName: 'Admin',
      email: 'admin@church.local',
      roles: ['Admin'],
      cellId: null,
      cellName: null,
      mustChangePassword: false
    }
  };

  let login: jasmine.Spy;
  let router: Router;

  beforeEach(async () => {
    login = jasmine.createSpy('login').and.returnValue(of(response));
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: { login } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => null } } }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  it('keeps the submit button disabled until credentials are present', () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();

    fixture.componentInstance.form.setValue({
      username: 'admin',
      password: 'secret',
      rememberMe: false
    });
    fixture.detectChanges();

    expect(button.disabled).toBeFalse();
  });

  it('submits the login request and navigates to the dashboard', () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.componentInstance.form.setValue({
      username: 'admin',
      password: 'secret',
      rememberMe: true
    });

    fixture.componentInstance.submit();

    expect(login).toHaveBeenCalledWith({
      username: 'admin',
      password: 'secret',
      rememberMe: true
    });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });
});
