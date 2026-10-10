import { enrolmentLanding } from './enrolment-landing';

describe('enrolmentLanding', () => {
  it('keeps the enrolment link with its invitation', () => {
    expect(enrolmentLanding('/admin/enroll?invitation=abc')).toBe('/admin/enroll?invitation=abc');
  });

  it.each(['/admin', '/admin/users?page=2', '/admin/enrollment'])(
    'sends any other page to the enrolment: %s',
    (landing) => {
      expect(enrolmentLanding(landing)).toBe('/admin/enroll');
    },
  );
});
