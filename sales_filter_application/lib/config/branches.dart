class CompanyBranch {
  CompanyBranch({required this.name, required this.value, required this.link});
  final String name;
  final String value;
  final String link;
}

class CompanyBranches {
  static List<CompanyBranch> defaults() => [
        CompanyBranch(
          name: 'النجف - DEMO',
          value: 'DatabaseCompanyNajaf_DEMO',
          link: 'http://169.58.236.52:8080/api/',
        ),
        CompanyBranch(
          name: 'محلي',
          value: 'local',
          link: 'http://127.0.0.1:5180/api/',
        ),
      ];
}
