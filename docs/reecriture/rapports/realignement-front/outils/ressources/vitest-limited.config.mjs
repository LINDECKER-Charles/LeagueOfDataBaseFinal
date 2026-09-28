// Caps Vitest parallelism for agents sharing one machine: the default is one worker per
// logical CPU (24 here), i.e. ~23 jsdom workers per `ng test`, which several agents multiply.
export default {
  test: {
    maxWorkers: 3,
    minWorkers: 1,
  },
};
