import { modeLabelsOf } from './mode-labels-of';

describe('modeLabelsOf', () => {
  it("names the modes by the whitelist's labels, in the API's order", () => {
    const modes = [
      { code: 'CLASSIC', facetable: true, label: "Summoner's Rift" },
      { code: 'ARAM', facetable: true, label: 'ARAM' },
    ];

    expect(modeLabelsOf(modes, 'LoL Classic')).toEqual(["Summoner's Rift", 'ARAM']);
  });

  it('names the LoL Classic client by its edition', () => {
    const modes = [{ code: 'JADE', facetable: false, label: null }];

    expect(modeLabelsOf(modes, 'LoL Classic')).toEqual(['LoL Classic']);
  });

  it('drops the modes the whitelist does not name, and a name already shown', () => {
    const modes = [
      { code: 'WIPMODEWIP', facetable: false },
      { code: 'CLASSIC', facetable: true, label: "Summoner's Rift" },
      { code: 'TUTORIAL', facetable: false, label: "Summoner's Rift" },
      { code: 'RUBY_TRIAL_1', facetable: false, label: '' },
    ];

    expect(modeLabelsOf(modes, 'LoL Classic')).toEqual(["Summoner's Rift"]);
  });
});
