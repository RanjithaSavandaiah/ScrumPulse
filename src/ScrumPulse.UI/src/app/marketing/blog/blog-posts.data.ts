/**
 * One article, addressable at /blog/:slug.
 *
 * Body is trusted HTML authored in this file, never user input, so it is bound with
 * [innerHTML] without sanitisation concerns. If posts ever move to a CMS or the database,
 * that binding must be revisited.
 */
export interface BlogPost {
  slug: string;
  title: string;

  /** Shown on the index and used as the page meta description. */
  summary: string;

  /** ISO date, used for display and for the article's published date. */
  publishedOn: string;

  /** Rough reading time in minutes, so the index sets expectations. */
  readingMinutes: number;

  /** Topic label, used for grouping on the index. */
  topic: string;

  /** Article body as HTML. */
  body: string;
}

/**
 * The articles published on the site.
 *
 * Written from the delivery practices this product actually implements rather than
 * assembled from generic agile advice, which is the distinction Google's reviewers are
 * looking for and the reason thin listicle content gets rejected.
 *
 * Kept as data rather than a component per post so adding an article is one entry here and
 * nothing else: the index, the routes and the sitemap all derive from this array.
 */
export const BLOG_POSTS: readonly BlogPost[] = [
  {
    slug: 'blocker-sla-why-eight-hours',
    title: 'Why we put an eight hour SLA on blockers, and what it changed',
    summary:
      'Most teams track blockers as a status. Treating them as a clock with a deadline changes who escalates, when, and how honest a standup becomes.',
    publishedOn: '2026-09-02',
    readingMinutes: 6,
    topic: 'Delivery metrics',
    body: `
      <p>
        Every team I have worked with tracks blockers. Almost none of them track how long a
        blocker has been blocking. That distinction sounds pedantic and it is the difference
        between a list nobody reads and a number that forces a decision.
      </p>

      <h2>A status tells you nothing about urgency</h2>
      <p>
        When a blocker is a boolean, every blocker looks the same. The one raised an hour ago
        sits in the same column as the one raised on Tuesday. In standup both get the same
        fifteen seconds, and the Tuesday one gets them for the fourth day running because
        nobody has a reason to treat it differently today than yesterday.
      </p>
      <p>
        What changes the conversation is elapsed time. "Blocked on the client API key, waiting
        nineteen hours" is a different sentence from "blocked on the client API key". The first
        one carries its own argument for escalation. Nobody has to make the case.
      </p>

      <h2>Why eight hours</h2>
      <p>
        We picked eight working hours, which is roughly one working day. The number matters
        less than the fact that it is shorter than people expect. A threshold of three days
        gets treated as permission to wait three days. A threshold of one day means a blocker
        surviving overnight is already a problem, and that is the behaviour we wanted.
      </p>
      <p>
        Counting working hours rather than wall-clock hours matters more than it sounds. A
        blocker raised at four on Friday afternoon is not twelve hours old on Saturday morning
        in any sense a team can act on. Counting only working time keeps the number honest, and
        it stops Monday morning starting with a screen of false breaches that people learn to
        ignore.
      </p>

      <h2>Categorise by cause, not by severity</h2>
      <p>
        We initially tagged blockers by severity and it told us nothing. Severity is a
        restatement of how loudly the blocked person complained. What produced useful signal
        was categorising by <em>cause</em>: waiting on client clarification, waiting on a tech
        lead decision, waiting on environment access, waiting on a third party.
      </p>
      <p>
        After a few sprints the pattern is usually unambiguous. One category will dominate, and
        it is almost never the one the team assumed. In our case a third of all blocked hours
        traced to environment access, which is not an agile problem, not a capacity problem,
        and not something any amount of standup discipline would have surfaced. It was a
        permissions process nobody owned.
      </p>

      <h2>What it changed</h2>
      <p>
        Three things, in order of how much they mattered.
      </p>
      <p>
        Escalation became unemotional. The clock escalates, not the person. That removes the
        social cost of saying a colleague or a client is holding you up, which is the real
        reason blockers go unreported.
      </p>
      <p>
        Retrospectives got a subject. "How was the sprint" produces vague answers. "We lost
        forty-one hours to environment access across four blockers" produces an action item.
      </p>
      <p>
        And the standup got shorter, because a blocker with a visible clock does not need to be
        re-explained every morning.
      </p>

      <h2>The failure mode to watch</h2>
      <p>
        An SLA becomes theatre the moment breaching it has no consequence. If blockers routinely
        sit at forty hours and nothing happens, the number has taught everyone that the number
        does not matter, and you are worse off than before you measured it. Either the threshold
        is one somebody acts on, or do not set one.
      </p>
    `
  },
  {
    slug: 'say-do-ratio-honest-forecasting',
    title: 'The say-do ratio is a forecasting tool, not a performance metric',
    summary:
      'Committed versus delivered is the most misused number in agile delivery. Used as a scorecard it corrupts estimation. Used as a forecast it is the most useful figure a team has.',
    publishedOn: '2026-08-21',
    readingMinutes: 7,
    topic: 'Delivery metrics',
    body: `
      <p>
        The say-do ratio is simply what the team committed to divided by what it delivered.
        Thirty points committed, twenty-four delivered, eighty percent. It is trivial to
        calculate and almost universally misused.
      </p>

      <h2>The moment it stops working</h2>
      <p>
        A say-do ratio becomes useless the instant it is read as a measure of how hard people
        worked. The feedback loop is immediate and entirely rational: if eighty percent gets
        you questioned and a hundred percent does not, commit to less. Within two sprints the
        team commits to what it is certain of, the ratio reads a beautiful one hundred percent,
        and it now tells you nothing at all because it measures the team's caution rather than
        its capacity.
      </p>
      <p>
        This is not a people problem to be managed around. It is the predictable result of
        measuring a forecast as though it were a promise.
      </p>

      <h2>What it is actually for</h2>
      <p>
        The ratio's value is that it lets you convert a future commitment into a probable
        outcome. A team that lands consistently at eighty percent is not underperforming. It is
        a team whose commitments should be read as eighty percent likely, which is genuinely
        useful when someone asks whether a feature lands this quarter.
      </p>
      <p>
        Consistency matters far more than the value. Eighty percent every sprint is a team you
        can plan around. Alternating between sixty and a hundred and twenty is not, even though
        the average looks better.
      </p>

      <h2>Adjust for capacity before drawing conclusions</h2>
      <p>
        Most apparent drops in say-do are capacity events wearing a disguise. Two people on
        leave and a public holiday is a thirty percent capacity reduction, and a sprint that
        delivers seventy percent of its commitment under those conditions has arguably
        outperformed one that delivered ninety at full strength.
      </p>
      <p>
        This is why we calculate net capacity from approved leave and working days before
        comparing anything. Comparing raw delivered points across sprints with different
        available hours produces a chart that trends according to the holiday calendar.
      </p>

      <h2>How to present it upwards</h2>
      <p>
        When a delivery lead asks how the sprint went, the temptation is to lead with the
        percentage. Do not. Lead with the forecast it supports.
      </p>
      <p>
        "We are landing around eighty percent of commitment consistently, we committed to
        thirty-two points next sprint, so plan for twenty-five" is a sentence a stakeholder can
        act on. "Our say-do was seventy-nine percent" invites the question of whose fault that
        is, which is the conversation you were trying to avoid.
      </p>

      <h2>When to actually worry</h2>
      <p>
        A single low sprint is noise. What deserves attention is a downward trend across three
        or more sprints while capacity holds steady, because that usually indicates accumulating
        drag: growing review latency, rising defect rework, or technical debt in whatever the
        team is currently working through. The ratio will not tell you which. It tells you to go
        and look, which is all a metric should ever do.
      </p>
    `
  },
  {
    slug: 'cycle-time-micro-stages',
    title: 'Splitting cycle time into micro-stages found the bottleneck in a week',
    summary:
      'A single cycle-time number tells you work is slow. Breaking the path from in-progress to done into seven timestamps tells you where, which is the only version you can act on.',
    publishedOn: '2026-08-07',
    readingMinutes: 6,
    topic: 'Engineering flow',
    body: `
      <p>
        Cycle time as normally measured is one number: how long from starting a item to
        finishing it. When that number is bad, the usual response is to ask the team to focus,
        which is advice with nowhere to land.
      </p>

      <h2>Seven timestamps instead of two</h2>
      <p>
        We instrumented the path a work item takes as a sequence of stages rather than a start
        and an end: backlog, in progress, PR created, PR approved, merged, in QA, done. Each
        transition writes a timestamp. Nobody fills in a form; the stage changes as part of work
        people were doing anyway.
      </p>
      <p>
        The interesting quantity is not the total. It is the gap between consecutive stages,
        because each gap has a different owner and a different remedy.
      </p>

      <h2>What showed up immediately</h2>
      <p>
        Development time - in progress to PR created - was fine. It was the number everyone
        assumed was the problem and it was not.
      </p>
      <p>
        The gap between PR created and PR approved was several times larger than any other
        stage. Work was not slow to build. It was slow to be looked at. That is a scheduling
        problem, not a capability problem, and the interventions for it are completely
        different: a fixed review window, reviewer assignment at creation, a WIP limit that
        counts open PRs.
      </p>
      <p>
        None of that would have been suggested by a single cycle-time figure, and the instinct
        it does provoke - asking developers to work faster - targets the one stage that was not
        contributing.
      </p>

      <h2>Measure in working hours</h2>
      <p>
        A PR raised at five on Friday and approved at nine on Monday took four working hours,
        not sixty-four. Measuring in wall-clock time makes every Friday afternoon look like a
        crisis and floods the data with noise that tracks the weekend rather than the team.
      </p>

      <h2>Averages hide the thing you want</h2>
      <p>
        Review latency averaging four hours sounds healthy until you look at the distribution
        and find most PRs are reviewed within thirty minutes and a handful sit for two days. The
        average describes neither group. The long tail is where the delivery risk lives, and an
        average is specifically designed to make tails disappear.
      </p>
      <p>
        Look at how many items breached a threshold, not at the mean.
      </p>

      <h2>A caution</h2>
      <p>
        Per-stage timings are per-item measurements, and it is a very short step from there to
        per-person measurements. Resist it. The moment review latency is read as a reviewer's
        score, PRs get approved quickly and read carefully by nobody, and you have optimised
        away the review itself. Keep the measurement on the work.
      </p>
    `
  },
  {
    slug: 'running-standups-across-timezones',
    title: 'What actually works for standups across timezones',
    summary:
      'Distributed standups fail for structural reasons, not motivational ones. Four changes that held up across an offshore split.',
    publishedOn: '2026-07-19',
    readingMinutes: 5,
    topic: 'Scrum practice',
    body: `
      <p>
        A standup works when the people in it can act on what they hear. Split a team across a
        four or five hour offset and that assumption quietly breaks, because by the time one
        half hears about a blocker the other half has gone home.
      </p>

      <h2>Protect the overlap rather than the ceremony</h2>
      <p>
        The scarcest resource in a distributed team is the window where everyone is awake. Most
        teams spend the first fifteen minutes of it on a status round that could have been read
        asynchronously.
      </p>
      <p>
        Inverting that helped more than anything else we tried: updates written down before the
        call, and the live window spent only on the things that need two people in a room -
        blockers, decisions, pairing. The meeting got shorter and considerably more useful.
      </p>

      <h2>Write the blocker down, with a clock on it</h2>
      <p>
        A blocker mentioned aloud at a time when the person who can unblock it is asleep has not
        been raised in any practical sense. Written blockers with a visible elapsed time survive
        the handover. The person coming online sees a nineteen-hour-old item, not a sentence in
        a meeting they missed.
      </p>

      <h2>Rotate who is inconvenienced</h2>
      <p>
        If the call is always at a time that suits one location, the other location is
        permanently the junior party regardless of anyone's intent. Rotating the awkward slot
        costs a little convenience and buys something worth more than the inconvenience.
      </p>

      <h2>Keep the timer, and keep it visible</h2>
      <p>
        Two minutes each sounds mechanical and it is the single change people thank you for
        later. It is not about efficiency. A visible, equal allocation means the quietest person
        gets the same space as the most talkative one, and on a distributed call, where social
        cues are thin and the person on poor audio simply loses, that equality does not happen
        on its own.
      </p>

      <h2>What did not work</h2>
      <p>
        Recording the call for the absent half. Nobody watched them. A twelve-minute recording
        asks more of someone's morning than a written summary and delivers less, and after three
        weeks the recordings were being made and not opened by anyone.
      </p>
    `
  }
];

/** Finds a post by its URL slug. */
export function findPostBySlug(slug: string): BlogPost | undefined {
  return BLOG_POSTS.find((post) => post.slug === slug);
}

/** Posts newest first, for the index page. */
export function postsNewestFirst(): BlogPost[] {
  return [...BLOG_POSTS].sort((first, second) => second.publishedOn.localeCompare(first.publishedOn));
}
