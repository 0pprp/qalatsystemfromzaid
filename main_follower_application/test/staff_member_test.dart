import 'package:flutter_test/flutter_test.dart';
import 'package:main_follower_application/domain/staff_member.dart';

void main() {
  test('parses delegate / follower / employee', () {
    final d = StaffMember.delegate({
      'delegateId': 12,
      'delegateName': 'أحمد',
      'phone': '07',
    });
    expect(d.targetType, 'Delegate');
    expect(d.id, 12);
    expect(d.listId, 12);

    final f = StaffMember.follower({
      'followerId': 3,
      'followerName': 'سارة',
      'cityName': 'البصرة',
    });
    expect(f.targetType, 'SubFollower');
    expect(f.id, 3);

    final e = StaffMember.employee({
      'employeeId': 9,
      'employeeName': 'علي',
      'shiftStatus': 'Active',
    });
    expect(e.targetType, 'SalesEmployee');
    expect(e.id, 9);
  });

  test('staff review fromJson', () {
    final r = StaffReview.fromJson({
      'id': 1,
      'missionId': 2,
      'cityValue': 'x',
      'targetType': 'Delegate',
      'targetId': 5,
      'score': 8,
      'targetName': 'أحمد',
      'note': 'جيد',
      'feedNote': 'feed',
    });
    expect(r.typeLabel, 'مندوب');
    expect(r.score, 8);
  });
}
